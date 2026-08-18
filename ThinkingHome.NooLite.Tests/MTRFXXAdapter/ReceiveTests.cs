using System.Globalization;
using System.Threading.Tasks;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Спека packet-receiving: порядок публикации (общее событие → типизированное), порядок
/// пакетов, медленный обработчик не мешает приёму, битый пакет → Error.
/// Общий контекст: открытый адаптер на подставном порту; байты подкладываются через
/// <c>Feed</c>, таймер адаптера (50 мс) вычитывает их, диспетчер вызывает события; все события
/// пишутся в один журнал <see cref="EventLog"/>, по нему проверяется состав и порядок.
/// </summary>
[Collection(AdapterCollection.Name)]
public class ReceiveTests
{
    private static (NooLite.MTRFXXAdapter adapter, FakeSerialDevice port, EventLog log) OpenAdapter()
    {
        var port = new FakeSerialDevice();
        var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();

        adapter.ReceiveData += (_, d) => log.Add($"data:{d.Command}/{d.DataFormat}/{d.Data1}");
        adapter.ReceivePowerUnitState += (_, d) => log.Add($"state:{d.State}");
        adapter.ReceiveStateFormatError += (_, d) => log.Add($"fmt-error:{d.DataFormat}");
        adapter.ReceiveMicroclimateData += (_, d) =>
            log.Add($"climate:{d.Temperature.ToString(CultureInfo.InvariantCulture)}/{d.Humidity}");
        adapter.Error += (_, ex) => log.Add($"error:{ex.GetType().Name}");

        adapter.Open();
        return (adapter, port, log);
    }

    /// <summary>
    /// Что: для Send_State с FMT 0 сначала срабатывает общее <c>ReceiveData</c>, затем
    /// <c>ReceivePowerUnitState</c> с разобранным состоянием (включён); других событий нет.
    /// Контекст: один пакет от блока 33347 с данными [5, 0, 1, 255].
    /// Спека: packet-receiving → «Пакет состояния блока», «Порядок публикации».
    /// </summary>
    [Fact]
    public async Task SendStateFmt0_RaisesReceiveData_ThenPowerUnitState()
    {
        var (adapter, port, log) = OpenAdapter();
        using (adapter)
        {
            port.Feed(Packets.SendStateFmt0());
            await log.WaitForCount(2);
            await Task.Delay(Wait.Grace);

            Assert.Equal(new[] { "data:SendState/0/5", $"state:{PowerUnitState.On}" }, log.Items);
        }
    }

    /// <summary>
    /// Что: для Send_State с FMT 255 — <c>ReceiveData</c>, затем <c>ReceiveStateFormatError</c>;
    /// событие состояния блока не срабатывает.
    /// Контекст: ответ блока на запрос несуществующей строки таблицы.
    /// Спека: packet-receiving → «Пакет ошибки формата», «Ошибочный формат ответа состояния».
    /// </summary>
    [Fact]
    public async Task SendStateFmt255_RaisesReceiveData_ThenStateFormatError()
    {
        var (adapter, port, log) = OpenAdapter();
        using (adapter)
        {
            port.Feed(Packets.SendStateFmt255());
            await log.WaitForCount(2);
            await Task.Delay(Wait.Grace);

            Assert.Equal(new[] { "data:SendState/255/0", "fmt-error:255" }, log.Items);
        }
    }

    /// <summary>
    /// Что: для Send_State с FMT 16 (иная строка таблицы) срабатывает только <c>ReceiveData</c> —
    /// ни состояния, ни ошибки формата.
    /// Контекст: после положительного сигнала выдерживается пауза, чтобы убедиться, что
    /// типизированные события не пришли следом.
    /// Спека: packet-receiving → «Ответ на запрос строки 16».
    /// </summary>
    [Fact]
    public async Task SendStateFmt16_RaisesOnlyReceiveData()
    {
        var (adapter, port, log) = OpenAdapter();
        using (adapter)
        {
            port.Feed(Packets.SendStateFmt16());
            await log.WaitForCount(1);
            await Task.Delay(Wait.Grace);

            Assert.Equal(new[] { "data:SendState/16/0" }, log.Items);
        }
    }

    /// <summary>
    /// Что: для Sens_Temp_Humi (21) с FMT 7 — <c>ReceiveData</c>, затем <c>ReceiveMicroclimateData</c>
    /// с разобранными 29,6 °C и 41 %.
    /// Контекст: пакет PT-111 с канала 1, данные [40, 33, 41, 255] (как на живом датчике).
    /// Спека: packet-receiving → «Пакет микроклимата».
    /// </summary>
    [Fact]
    public async Task Microclimate_RaisesReceiveData_ThenMicroclimateData()
    {
        var (adapter, port, log) = OpenAdapter();
        using (adapter)
        {
            port.Feed(Packets.Microclimate());
            await log.WaitForCount(2);
            await Task.Delay(Wait.Grace);

            Assert.Equal(new[] { "data:MicroclimateData/7/40", "climate:29.6/41" }, log.Items);
        }
    }

    /// <summary>
    /// Что: три пакета, подложенные одним <c>Feed</c>, доходят до обработчика в порядке A, B, C,
    /// хотя обработчик первого какое-то время стоял.
    /// Контекст: обработчик блокируется на <see cref="Gate"/> при первом пакете и отпускается
    /// после того, как остальные уже лежат в очереди.
    /// Спека: packet-receiving → «Порядок при нескольких пакетах».
    /// </summary>
    [Fact]
    public async Task SeveralPackets_WithSlowHandler_ArriveInOrder()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();
        var gate = new Gate();
        adapter.ReceiveData += gate.Handle;
        adapter.ReceiveData += (_, d) => log.Add($"data:{d.Data1}");
        adapter.Open();

        port.Feed(Packets.Untyped(1), Packets.Untyped(2), Packets.Untyped(3));
        await gate.WaitStarted();
        gate.Release();

        await log.WaitForCount(3);
        Assert.Equal(new[] { "data:1", "data:2", "data:3" }, log.Items);
    }

    /// <summary>
    /// Что: пока обработчик стоит на пакете A, новые пакеты B, C, D вычитываются из порта в очередь
    /// (буфер порта пустеет), а после отпускания доставляются по порядку A, B, C, D.
    /// Контекст: обработчик заблокирован на A; в этот момент подкладываются B, C, D; проверяется,
    /// что порт опустел до отпускания и что никто, кроме A, ещё не доставлен.
    /// Спека: packet-receiving → «Медленный обработчик и приём».
    /// </summary>
    [Fact]
    public async Task SlowHandler_DoesNotBlockReadingFromPort()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();
        var gate = new Gate();
        adapter.ReceiveData += gate.Handle;
        adapter.ReceiveData += (_, d) => log.Add($"data:{d.Data1}");
        adapter.Open();

        port.Feed(Packets.Untyped(1));
        await gate.WaitStarted();

        // обработчик стоит на A; B, C, D должны быть вычитаны из порта, не дожидаясь его
        port.Feed(Packets.Untyped(2), Packets.Untyped(3), Packets.Untyped(4));
        await Wait.Until(() => port.BytesToRead == 0, "port buffer drained while handler is blocked");
        Assert.Equal(1, gate.Entered);
        Assert.Equal(0, log.Count); // обработчик A ещё не завершился - никто не доставлен

        gate.Release();
        await log.WaitForCount(4);
        Assert.Equal(new[] { "data:1", "data:2", "data:3", "data:4" }, log.Items);
    }

    /// <summary>
    /// Что: пакет с испорченным стоповым маркером не роняет диспетчер — <c>Parse</c> бросает
    /// <c>ArgumentException</c>, она уходит в <c>Error</c>, следующий корректный пакет доставляется,
    /// адаптер остаётся открытым.
    /// Контекст: до async-receive битый пакет закрывал адаптер через <c>ThreadSafeExec</c>; теперь
    /// разбор идёт в диспетчере под <c>try</c>.
    /// Спека: packet-receiving → «Пакет с нарушенной рамкой» (в контексте диспетчера).
    /// </summary>
    [Fact]
    public async Task BrokenPacket_RaisesError_AndNextPacketIsDelivered()
    {
        var (adapter, port, log) = OpenAdapter();
        using (adapter)
        {
            port.Feed(Packets.Broken(), Packets.Untyped(7));
            await log.WaitForCount(2);

            Assert.Equal(new[] { "error:ArgumentException", "data:On/0/7" }, log.Items);
            Assert.True(adapter.IsOpened);
        }
    }

    /// <summary>
    /// Что: байты до стартового маркера пропускаются, пакет за ними разбирается и доставляется,
    /// буфер порта после этого пуст.
    /// Контекст: <c>TryRead</c> читает по байту, пока не встретит 173, — существующее поведение,
    /// спекой не описано.
    /// </summary>
    [Fact]
    public async Task GarbageBeforeStartMarker_IsSkipped()
    {
        var (adapter, port, log) = OpenAdapter();
        using (adapter)
        {
            port.Feed(new byte[] { 0, 1, 2 }, Packets.Untyped(9));
            await log.WaitForCount(1);
            await Task.Delay(Wait.Grace);

            Assert.Equal(new[] { "data:On/0/9" }, log.Items);
            Assert.Equal(0, port.BytesToRead);
        }
    }
}
