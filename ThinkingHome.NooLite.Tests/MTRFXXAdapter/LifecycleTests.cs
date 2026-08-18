using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Конструктор, Open/Close/Dispose, повторное открытие, отказ порта при опросе.
/// Всё это — существующее поведение адаптера; спеки на жизненный цикл нет
/// (см. proposal → Capabilities), тесты фиксируют то, что код делает сегодня.
/// </summary>
[Collection(AdapterCollection.Name)]
public class LifecycleTests
{
    /// <summary>
    /// Что: конструктор с пустой ссылкой вместо порта бросает <see cref="ArgumentNullException"/>.
    /// Контекст: адаптер ещё не создан; проверяется валидация аргумента внутреннего конструктора.
    /// </summary>
    [Fact]
    public void Constructor_NullDevice_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new NooLite.MTRFXXAdapter((ISerialDevice)null));
    }

    /// <summary>
    /// Что: ёмкость очереди 0 или отрицательная отвергается <see cref="ArgumentOutOfRangeException"/>.
    /// Контекст: та же проверка, что и у публичного строкового конструктора (packet-receiving →
    /// «Ограниченная очередь»), но через шов — без реального SerialPort.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveCapacity_Throws(int capacity)
    {
        var port = new FakeSerialDevice();

        Assert.Throws<ArgumentOutOfRangeException>(() => new NooLite.MTRFXXAdapter(port, capacity));
    }

    /// <summary>
    /// Что: <c>Open()</c> открывает порт, выставляет <c>IsOpened</c> и вызывает <c>Connect</c> ровно один раз.
    /// Контекст: свежесозданный закрытый адаптер.
    /// </summary>
    [Fact]
    public void Open_RaisesConnect_AndOpensPort()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var connects = 0;
        adapter.Connect += _ => connects++;

        Assert.False(adapter.IsOpened);

        adapter.Open();

        Assert.True(adapter.IsOpened);
        Assert.True(port.IsOpen);
        Assert.Equal(1, connects);
    }

    /// <summary>
    /// Что: <c>Close()</c> закрывает порт синхронно и вызывает <c>Disconnect</c> ровно один раз —
    /// но <b>асинхронно</b>, из потока диспетчера (событие ждём через сигнал, не сразу после вызова).
    /// Контекст: открытый адаптер с пустой очередью.
    /// </summary>
    [Fact]
    public async Task Close_RaisesDisconnect_AndClosesPort()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var disconnects = 0;
        var disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        adapter.Disconnect += _ =>
        {
            disconnects++;
            disconnected.TrySetResult();
        };
        adapter.Open();

        adapter.Close();

        Assert.False(adapter.IsOpened);
        Assert.False(port.IsOpen);
        await Wait.For(disconnected.Task, "Disconnect");
        await Task.Delay(Wait.Grace);
        Assert.Equal(1, disconnects);
    }

    /// <summary>
    /// Что: повторный <c>Open()</c> на уже открытом адаптере ничего не делает — ни второго
    /// <c>Connect</c>, ни второго открытия порта.
    /// Контекст: <c>ThreadSafeExec</c> проверяет <c>IsOpen</c> до и внутри замка.
    /// </summary>
    [Fact]
    public void Open_Twice_RaisesConnectOnce()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var connects = 0;
        adapter.Connect += _ => connects++;

        adapter.Open();
        adapter.Open();

        Assert.Equal(1, connects);
        Assert.Equal(1, port.OpenCount);
    }

    /// <summary>
    /// Что: <c>Close()</c> на закрытом адаптере ничего не делает — нет <c>Disconnect</c>, порт не трогается.
    /// Контекст: адаптер создан, но не открывался.
    /// </summary>
    [Fact]
    public void Close_WhenNotOpen_DoesNothing()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var disconnects = 0;
        adapter.Disconnect += _ => disconnects++;

        adapter.Close();

        Assert.Equal(0, disconnects);
        Assert.Equal(0, port.CloseCount);
    }

    /// <summary>
    /// Что: двойной <c>Dispose()</c> безопасен и оставляет адаптер закрытым.
    /// Контекст: открытый адаптер; второй Dispose попадает на уже закрытый порт, завершённый
    /// канал и освобождённый таймер.
    /// </summary>
    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var port = new FakeSerialDevice();
        var adapter = new NooLite.MTRFXXAdapter(port);
        adapter.Open();

        adapter.Dispose();
        adapter.Dispose();

        Assert.False(adapter.IsOpened);
    }

    /// <summary>
    /// Что: после <c>Close()</c> и повторного <c>Open()</c> пакеты снова доходят до обработчиков —
    /// один диспетчер живёт весь жизненный цикл, флаг <c>closing</c> сбрасывается при Open.
    /// Контекст: пакет 1 доставлен до закрытия, пакет 2 подложен после повторного открытия.
    /// Спека: packet-receiving → закрытие; async-receive/report → 3.6 (reopen на железе).
    /// </summary>
    [Fact]
    public async Task Reopen_DeliversPacketsAgain()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();
        adapter.ReceiveData += (_, d) => log.Add($"data:{d.Data1}");

        adapter.Open();
        port.Feed(Packets.Untyped(1));
        await log.WaitForCount(1);

        adapter.Close();
        adapter.Open();
        port.Feed(Packets.Untyped(2));
        await log.WaitForCount(2);

        Assert.Equal(new[] { "data:1", "data:2" }, log.Items);
    }

    /// <summary>
    /// Что: если порт бросил исключение при опросе (<c>BytesToRead</c>), адаптер сообщает о нём
    /// через <c>Error</c> и закрывает себя: порт закрыт, <c>IsOpened == false</c>, <c>Disconnect</c>
    /// вызван; отправка после этого невозможна.
    /// Контекст: открытый адаптер, порт отваливается на очередном тике таймера
    /// (<c>TryRead</c> → <c>ThreadSafeExec</c> с <c>errorHandler: Close</c>). Существующее поведение,
    /// спекой не описано.
    /// </summary>
    [Fact]
    public async Task ReadFailure_RaisesError_AndClosesAdapter()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var error = new TaskCompletionSource<Exception>();
        var disconnected = new TaskCompletionSource();
        adapter.Error += (_, ex) => error.TrySetResult(ex);
        adapter.Disconnect += _ => disconnected.TrySetResult();
        adapter.Open();

        var failure = new IOException("port is gone");
        port.FailRead = failure;

        var raised = await Wait.For(error.Task, "Error event");
        await Wait.For(disconnected.Task, "Disconnect event");

        Assert.Same(failure, raised);
        Assert.False(adapter.IsOpened);
        Assert.False(port.IsOpen);

        // после закрытия отправка невозможна, пока адаптер не открыт снова
        Assert.Throws<InvalidOperationException>(() =>
            adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, 0, MTRFXXCommand.On));
    }
}
