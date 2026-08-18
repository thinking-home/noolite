using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Спека packet-receiving → «Закрытие адаптера и судьба очереди».
/// Расстановка для тестов с очередью (<see cref="Setup.ArrangeBlockedWithQueue"/>): обработчик
/// стоит на P1 (пакет без типизированного разбора — см. design → Risks), P2 и P3 уже вычитаны
/// из порта и лежат в очереди. Журнал пишет «data:N» на входе в обработчик и «disconnect»
/// в момент события отключения — по нему видно, что дошло до обработчиков и что было после
/// Disconnect.
/// </summary>
[Collection(AdapterCollection.Name)]
public class CloseTests
{
    private sealed class Setup : IDisposable
    {
        public readonly FakeSerialDevice Port = new();
        public readonly NooLite.MTRFXXAdapter Adapter;
        public readonly EventLog Log = new();
        public readonly Gate Gate = new();
        public readonly TaskCompletionSource Disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Setup()
        {
            Adapter = new NooLite.MTRFXXAdapter(Port);
            // журнал - до Gate: запись "пакет дошёл до обработчика" делается на входе,
            // а не после отпускания (иначе для P1 она встала бы в журнал после disconnect)
            Adapter.ReceiveData += (_, d) => Log.Add($"data:{d.Data1}");
            Adapter.ReceiveData += Gate.Handle;
            Adapter.Disconnect += _ =>
            {
                Log.Add("disconnect");
                Disconnected.TrySetResult();
            };
            Adapter.Open();
        }

        /// <summary>P1 в обработчике, P2 и P3 в очереди.</summary>
        public async Task ArrangeBlockedWithQueue()
        {
            Port.Feed(Packets.Untyped(1));
            await Gate.WaitStarted();

            Port.Feed(Packets.Untyped(2), Packets.Untyped(3));
            await Wait.Until(() => Port.BytesToRead == 0, "P2, P3 read into the queue");
        }

        public void Dispose()
        {
            Gate.Release();
            Adapter.Dispose();
        }
    }

    /// <summary>
    /// Что: <c>Close()</c> при непустой очереди закрывает порт сразу, вызывает <c>Disconnect</c>,
    /// а P2, P3 до обработчиков не доходят — ни до, ни после отпускания обработчика P1.
    /// Контекст: P1 в обработчике, P2, P3 в очереди; после <c>Disconnect</c> обработчик отпускается
    /// и выдерживается пауза — журнал должен остаться «data:1, disconnect».
    /// Спека: packet-receiving → «Немедленное закрытие с непустой очередью».
    /// </summary>
    [Fact]
    public async Task Close_WithPendingPackets_DropsThem_DisconnectIsLast()
    {
        using var s = new Setup();
        await s.ArrangeBlockedWithQueue();

        s.Adapter.Close();

        Assert.False(s.Port.IsOpen);
        Assert.False(s.Adapter.IsOpened);
        await Wait.For(s.Disconnected.Task, "Disconnect");

        s.Gate.Release();
        await Task.Delay(Wait.Grace);

        Assert.Equal(new[] { "data:1", "disconnect" }, s.Log.Items);
    }

    /// <summary>
    /// Что: <c>FlushAndCloseAsync()</c> закрывает порт немедленно, но не завершается и не зовёт
    /// <c>Disconnect</c>, пока очередь не обработана; после отпускания обработчика P1, P2, P3
    /// доставляются по порядку, затем <c>Disconnect</c>, затем задача завершается.
    /// Контекст: P1 в обработчике, P2, P3 в очереди; незавершённость задачи проверяется после
    /// паузы, чтобы исключить «вернулся сразу».
    /// Спека: packet-receiving → «Закрытие с обработкой остатка».
    /// </summary>
    [Fact]
    public async Task FlushAndClose_DeliversPending_ThenDisconnects()
    {
        using var s = new Setup();
        await s.ArrangeBlockedWithQueue();

        var flush = s.Adapter.FlushAndCloseAsync();

        // порт закрыт сразу, но метод ждёт остаток очереди
        Assert.False(s.Port.IsOpen);
        await Task.Delay(Wait.Grace);
        Assert.False(flush.IsCompleted);
        Assert.False(s.Disconnected.Task.IsCompleted);

        s.Gate.Release();
        await Wait.For(flush, "FlushAndCloseAsync");

        Assert.Equal(new[] { "data:1", "data:2", "data:3", "disconnect" }, s.Log.Items);
        Assert.False(s.Adapter.IsOpened);
    }

    /// <summary>
    /// Что: отмена токена во время ожидания <c>FlushAndCloseAsync</c> — задача завершается
    /// <see cref="OperationCanceledException"/>, <c>Disconnect</c> вызывается, порт остаётся закрытым,
    /// остаток очереди (P2, P3) отбрасывается и после отпускания обработчика не доставляется.
    /// Контекст: P1 в обработчике, P2, P3 в очереди; токен отменяется, пока обработчик стоит.
    /// Спека: packet-receiving → «Отмена ожидания».
    /// </summary>
    [Fact]
    public async Task FlushAndClose_Cancelled_DropsRemainder_Disconnects()
    {
        using var s = new Setup();
        await s.ArrangeBlockedWithQueue();
        using var cts = new CancellationTokenSource();

        var flush = s.Adapter.FlushAndCloseAsync(cts.Token);
        Assert.False(s.Port.IsOpen);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Wait.For(flush, "cancelled flush"));
        await Wait.For(s.Disconnected.Task, "Disconnect after cancel");
        Assert.False(s.Port.IsOpen);

        s.Gate.Release();
        await Task.Delay(Wait.Grace);

        Assert.Equal(new[] { "data:1", "disconnect" }, s.Log.Items);
    }

    /// <summary>
    /// Что: синхронное ожидание <c>FlushAndCloseAsync()</c> изнутри обработчика события завершается
    /// <see cref="InvalidOperationException"/> (а не взаимной блокировкой), адаптер остаётся открытым.
    /// Контекст: обработчик <c>ReceiveData</c> вызывает метод и ждёт результат; исключение
    /// передаётся в тест через <c>TaskCompletionSource</c>. Защита — <c>AsyncLocal insideHandler</c>.
    /// Спека: packet-receiving → «Закрытие с обработкой остатка из обработчика».
    /// </summary>
    [Fact]
    public async Task FlushAndClose_FromHandler_Throws()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var result = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        adapter.ReceiveData += (_, _) =>
        {
            try
            {
                adapter.FlushAndCloseAsync().GetAwaiter().GetResult();
                result.TrySetResult(null);
            }
            catch (Exception ex)
            {
                result.TrySetResult(ex);
            }
        };
        adapter.Open();

        port.Feed(Packets.Untyped());
        var thrown = await Wait.For(result.Task, "handler result");

        Assert.IsType<InvalidOperationException>(thrown);
        Assert.True(adapter.IsOpened); // адаптер не закрылся
    }

    /// <summary>
    /// Что: при пустой очереди и простаивающем диспетчере <c>FlushAndCloseAsync()</c> возвращается,
    /// не зависая, и вызывает <c>Disconnect</c>.
    /// Контекст: ветка «ждать нечего» — сигнала от диспетчера не будет (он спит на
    /// <c>WaitToReadAsync</c>), метод должен это распознать по <c>Count == 0 &amp;&amp; inFlight == 0</c>.
    /// </summary>
    [Fact]
    public async Task FlushAndClose_WithEmptyQueue_ReturnsAndDisconnects()
    {
        using var s = new Setup();

        await Wait.For(s.Adapter.FlushAndCloseAsync(), "FlushAndCloseAsync on idle adapter");

        Assert.Equal(new[] { "disconnect" }, s.Log.Items);
        Assert.False(s.Adapter.IsOpened);
    }

    /// <summary>
    /// Что: <c>FlushAndCloseAsync()</c> на неоткрытом адаптере возвращается без <c>Disconnect</c>.
    /// Контекст: ветка <c>wasOpen == false</c> — закрывать нечего, событий быть не должно.
    /// </summary>
    [Fact]
    public async Task FlushAndClose_WhenNotOpen_ReturnsWithoutDisconnect()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var disconnects = 0;
        adapter.Disconnect += _ => disconnects++;

        await Wait.For(adapter.FlushAndCloseAsync(), "FlushAndCloseAsync on closed adapter");

        Assert.Equal(0, disconnects);
    }

    /// <summary>
    /// Что: <c>Dispose()</c> при непустой очереди ведёт себя как <c>Close()</c>: порт закрыт,
    /// <c>Disconnect</c> вызван, P2, P3 отброшены и после отпускания обработчика не доставляются.
    /// Контекст: P1 в обработчике, P2, P3 в очереди; адаптер освобождается напрямую, без using.
    /// Спека: packet-receiving → «Освобождение адаптера».
    /// </summary>
    [Fact]
    public async Task Dispose_WithPendingPackets_BehavesLikeClose()
    {
        var s = new Setup();
        await s.ArrangeBlockedWithQueue();

        s.Adapter.Dispose();

        Assert.False(s.Port.IsOpen);
        await Wait.For(s.Disconnected.Task, "Disconnect");

        s.Gate.Release();
        await Task.Delay(Wait.Grace);

        Assert.Equal(new[] { "data:1", "disconnect" }, s.Log.Items);
    }
}
