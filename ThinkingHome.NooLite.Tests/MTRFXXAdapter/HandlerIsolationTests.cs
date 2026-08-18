using System;
using System.Threading.Tasks;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Спека packet-receiving → «Изоляция обработчиков»: исключение в одном обработчике
/// не мешает остальным обработчикам того же пакета, следующим пакетам и работе адаптера.
/// Контекст: открытый адаптер на подставном порту, события пишутся в общий журнал.
/// </summary>
[Collection(AdapterCollection.Name)]
public class HandlerIsolationTests
{
    /// <summary>
    /// Что: если обработчик <c>ReceiveData</c> бросил на пакете Send_State FMT 0, то исключение
    /// (тот же объект) приходит в <c>Error</c>, типизированное <c>ReceivePowerUnitState</c> того же
    /// пакета всё равно вызывается, следующий пакет доставляется, адаптер остаётся открытым.
    /// Контекст: два пакета подряд — Send_State (обработчик бросает) и обычный (не бросает).
    /// До async-receive такое исключение закрывало адаптер.
    /// Спека: packet-receiving → «Обработчик бросил исключение».
    /// </summary>
    [Fact]
    public async Task ThrowingHandler_GoesToError_TypedEventStillRaised_NextPacketDelivered()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();
        var failure = new InvalidOperationException("handler failed");

        adapter.ReceiveData += (_, d) =>
        {
            log.Add($"data:{d.Data1}");
            if (d.Command == Internal.MTRFXXCommand.SendState) throw failure;
        };
        adapter.ReceivePowerUnitState += (_, d) => log.Add($"state:{d.State}");
        adapter.Error += (_, ex) => log.Add($"error:{ReferenceEquals(ex, failure)}");
        adapter.Open();

        port.Feed(Packets.SendStateFmt0(), Packets.Untyped(7));
        await log.WaitForCount(4);

        Assert.Equal(new[] { "data:5", "error:True", $"state:{PowerUnitState.On}", "data:7" }, log.Items);
        Assert.True(adapter.IsOpened);
    }

    /// <summary>
    /// Что: если бросает сам обработчик <c>Error</c>, исключение подавляется, доставка продолжается —
    /// следующий пакет доходит до обработчика, адаптер открыт.
    /// Контекст: обработчик <c>ReceiveData</c> бросает на первом пакете, обработчик <c>Error</c>
    /// бросает в ответ; второй пакет обычный.
    /// Спека: packet-receiving → «Обработчик ошибки бросил исключение».
    /// </summary>
    [Fact]
    public async Task ThrowingErrorHandler_IsSwallowed_DeliveryContinues()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();

        adapter.ReceiveData += (_, d) =>
        {
            log.Add($"data:{d.Data1}");
            if (d.Data1 == 1) throw new InvalidOperationException("handler failed");
        };
        adapter.Error += (_, _) =>
        {
            log.Add("error");
            throw new InvalidOperationException("error handler failed too");
        };
        adapter.Open();

        port.Feed(Packets.Untyped(1), Packets.Untyped(2));
        await log.WaitForCount(3);

        Assert.Equal(new[] { "data:1", "error", "data:2" }, log.Items);
        Assert.True(adapter.IsOpened);
    }
}
