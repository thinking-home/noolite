using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Спека command-sending: атомарность записи, ошибка → исключение вызывающему,
/// отправка не ждёт обработчиков приёма.
/// </summary>
[Collection(AdapterCollection.Name)]
public class SendCommandTests
{
    private const byte START_MARKER = 171;
    private const byte STOP_MARKER = 172;

    /// <summary>
    /// Что: пакеты, отправленные одновременно из 8 потоков (по 40 на поток), приходят в порт
    /// целыми и неделимыми — в журнале ровно 320 × 17 байт, у каждого среза верные маркеры
    /// и контрольная сумма, а метка потока в канале, данных и адресе одна и та же.
    /// Контекст: подставной порт пишет по байту с <c>Thread.Yield()</c>, расширяя окно гонки;
    /// без <c>lock</c> в <c>SendCommand</c> тест падает на маркерах (проверено, см. report.md).
    /// Спека: command-sending → «Две команды из разных потоков».
    /// </summary>
    [Fact]
    public void SendCommand_FromManyThreads_WritesWholePackets()
    {
        const int threads = 8;
        const int packetsPerThread = 40;

        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        adapter.Open();

        using var start = new ManualResetEventSlim(false);

        var workers = Enumerable.Range(1, threads).Select(n => new Thread(() =>
        {
            var tag = (byte)n;
            start.Wait();

            for (var i = 0; i < packetsPerThread; i++)
                // метка потока - в канале, во всех четырёх байтах данных и в адресе:
                // если байты двух пакетов перемешаются, метки внутри среза разойдутся
                adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, tag, MTRFXXCommand.On,
                    MTRFXXRepeatCount.NoRepeat, MTRFXXDataFormat.FourByteData,
                    new[] { tag, tag, tag, tag }, (uint)(tag << 24 | tag << 16 | tag << 8 | tag));
        })).ToArray();

        foreach (var w in workers) w.Start();
        start.Set();
        foreach (var w in workers) w.Join();

        var written = port.Written;
        Assert.Equal(threads * packetsPerThread * 17, written.Length);

        foreach (var packet in port.WrittenPackets)
        {
            Assert.Equal(START_MARKER, packet[0]);
            Assert.Equal(STOP_MARKER, packet[16]);

            var checksum = (byte)packet.Take(15).Sum(b => b);
            Assert.Equal(checksum, packet[15]);

            var tag = packet[4];
            Assert.InRange(tag, 1, threads);
            Assert.All(packet[7..15], b => Assert.Equal(tag, b));
        }

        // от каждого потока - ровно его число пакетов
        var byTag = port.WrittenPackets.GroupBy(p => p[4]).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(threads, byTag.Count);
        Assert.All(byTag.Values, count => Assert.Equal(packetsPerThread, count));
    }

    /// <summary>
    /// Что: исключение при записи в порт доходит до вызывающего <c>SendCommand</c> тем же
    /// объектом, не превращается в событие <c>Error</c> и не закрывает адаптер.
    /// Контекст: открытый адаптер, порт настроен бросать <see cref="IOException"/> на <c>Write</c>.
    /// Спека: command-sending → «Ошибка ввода-вывода при записи», «Сообщение об ошибке отправки».
    /// </summary>
    [Fact]
    public void SendCommand_WriteFails_ThrowsToCaller_AndAdapterStaysOpen()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var errors = 0;
        adapter.Error += (_, _) => errors++;
        adapter.Open();

        var failure = new IOException("write failed");
        port.FailWrite = failure;

        var thrown = Assert.Throws<IOException>(() =>
            adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, 0, MTRFXXCommand.On));

        Assert.Same(failure, thrown);
        Assert.True(adapter.IsOpened);
        Assert.Equal(0, errors); // ошибка отправки идёт вызывающему, а не в событие Error
    }

    /// <summary>
    /// Что: отправка на неоткрытом адаптере завершается исключением, в порт ничего не пишется.
    /// Контекст: адаптер создан, <c>Open()</c> не вызывался; подставной порт, как и SerialPort,
    /// бросает <see cref="InvalidOperationException"/> при записи в закрытый порт.
    /// Спека: command-sending → «Отправка при закрытом порту».
    /// </summary>
    [Fact]
    public void SendCommand_WhenPortClosed_Throws()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);

        Assert.Throws<InvalidOperationException>(() =>
            adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, 0, MTRFXXCommand.On));

        Assert.Empty(port.Written);
    }

    /// <summary>
    /// Что: <c>SendCommand</c> из другого потока возвращается, пока обработчик <c>ReceiveData</c>
    /// всё ещё стоит, — отправка не ждёт обработчиков приёма.
    /// Контекст: обработчик заблокирован на <see cref="Gate"/> после первого пакета; отправка
    /// запускается в отдельной задаче и должна завершиться до отпускания обработчика.
    /// Спека: command-sending → «Отправка во время работы медленного обработчика».
    /// </summary>
    [Fact]
    public async Task SendCommand_WhileHandlerIsBlocked_DoesNotWait()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var gate = new Gate();
        adapter.ReceiveData += gate.Handle;
        adapter.Open();

        port.Feed(Packets.Untyped());
        await gate.WaitStarted();

        // обработчик стоит; отправка из другого потока должна вернуться, не дожидаясь его
        var send = Task.Run(() =>
            adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, 0, MTRFXXCommand.Off));

        await Wait.For(send, "SendCommand while handler is blocked");

        Assert.Single(port.WrittenPackets);
        Assert.Equal(1, gate.Entered);

        gate.Release();
    }
}
