using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Спека packet-receiving → «Ограниченная очередь входящих пакетов».
/// Контекст обоих тестов: обработчик <c>ReceiveData</c> заблокирован на первом пакете (он уже
/// вынут диспетчером, очередь пуста), после чего в порт подкладывается больше пакетов, чем
/// вмещает очередь; таймер их вычитывает, лишние отбрасываются при постановке (DropWrite).
/// </summary>
[Collection(AdapterCollection.Name)]
public class QueueTests
{
    /// <summary>
    /// Что: при ёмкости 2 из четырёх подложенных пакетов P2..P5 в очередь встают P2, P3, а P4, P5
    /// отбрасываются; <c>DroppedPacketsCount == 2</c>; после отпускания обработчика доставлены
    /// ровно P1, P2, P3 в порядке прихода — уже принятые не вытесняются новыми.
    /// Контекст: P1 в обработчике, очередь пуста; счётчик проверяется дважды — сразу после
    /// вычитывания и после доставки (не растёт задним числом).
    /// Спека: packet-receiving → «Очередь переполнена», «Порядок при переполнении».
    /// </summary>
    [Fact]
    public async Task Overflow_DropsNewPackets_KeepsOrder_CountsDropped()
    {
        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port, queueCapacity: 2);
        var log = new EventLog();
        var gate = new Gate();
        adapter.ReceiveData += gate.Handle;
        adapter.ReceiveData += (_, d) => log.Add($"data:{d.Data1}");
        adapter.Open();

        // P1 вынут диспетчером и стоит в обработчике - очередь пуста
        port.Feed(Packets.Untyped(1));
        await gate.WaitStarted();

        // ёмкость 2: P2, P3 встают в очередь, P4, P5 отбрасываются
        port.Feed(Packets.Untyped(2), Packets.Untyped(3), Packets.Untyped(4), Packets.Untyped(5));
        await Wait.Until(() => port.BytesToRead == 0, "port buffer drained");
        Assert.Equal(2, adapter.DroppedPacketsCount);

        gate.Release();
        await log.WaitForCount(3);
        await Task.Delay(Wait.Grace);

        Assert.Equal(new[] { "data:1", "data:2", "data:3" }, log.Items);
        Assert.Equal(2, adapter.DroppedPacketsCount);
    }

    /// <summary>
    /// Что: ёмкость по умолчанию — 128: константа равна 128, и при 130 подложенных пакетах
    /// (обработчик стоит на отдельном первом) в очередь встают 128, отбрасываются 2; после
    /// отпускания доставлены 1 + 128 пакетов.
    /// Контекст: адаптер создан без указания ёмкости.
    /// Спека: packet-receiving → «Ёмкость по умолчанию».
    /// </summary>
    [Fact]
    public async Task DefaultCapacity_Is128()
    {
        Assert.Equal(128, NooLite.MTRFXXAdapter.DEFAULT_QUEUE_CAPACITY);

        var port = new FakeSerialDevice();
        using var adapter = new NooLite.MTRFXXAdapter(port);
        var log = new EventLog();
        var gate = new Gate();
        adapter.ReceiveData += gate.Handle;
        adapter.ReceiveData += (_, _) => log.Add("data");
        adapter.Open();

        port.Feed(Packets.Untyped());
        await gate.WaitStarted();

        // 130 пакетов при 128 местах: 128 в очередь, 2 отброшены
        port.Feed(Enumerable.Range(0, 130).Select(_ => Packets.Untyped()).ToArray());
        await Wait.Until(() => port.BytesToRead == 0, "port buffer drained");
        Assert.Equal(2, adapter.DroppedPacketsCount);

        gate.Release();
        await log.WaitForCount(1 + 128);
        await Task.Delay(Wait.Grace);
        Assert.Equal(1 + 128, log.Count);
    }
}
