using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests;

/// <summary>
/// Тесты адаптера ждут таймер (50 мс) и диспетчер в пуле потоков; чтобы нагрузочные тесты
/// не съедали время у тестов с таймаутами, все они выполняются в одной коллекции и
/// не параллельно с остальными.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class AdapterCollection
{
    public const string Name = "adapter";
}

/// <summary>Входящие пакеты для подставного порта: маркеры 173/174, поля по раскладке ReceivedData.</summary>
public static class Packets
{
    public const uint DEVICE_ID = 33347;

    public static byte[] Packet(MTRFXXMode mode, MTRFXXCommand command, byte format = 0, byte[] data = null,
        uint deviceId = 0, byte channel = 0)
    {
        var bytes = TestHelpers.GetBytes();

        bytes[1] = (byte)mode;
        bytes[4] = channel;
        bytes[5] = (byte)command;
        bytes[6] = format;

        if (data != null) Array.Copy(data, 0, bytes, 7, data.Length);

        bytes[11] = (byte)(deviceId >> 24);
        bytes[12] = (byte)(deviceId >> 16);
        bytes[13] = (byte)(deviceId >> 8);
        bytes[14] = (byte)deviceId;

        return bytes;
    }

    /// <summary>Send_State, FMT 0 — основная информация о блоке (тип 5, включён, мощность 255).</summary>
    public static byte[] SendStateFmt0(uint deviceId = DEVICE_ID) =>
        Packet(MTRFXXMode.TXF, MTRFXXCommand.SendState, 0, new byte[] { 5, 0, 1, 255 }, deviceId);

    /// <summary>Send_State, FMT 255 — ошибка формата.</summary>
    public static byte[] SendStateFmt255(uint deviceId = DEVICE_ID) =>
        Packet(MTRFXXMode.TXF, MTRFXXCommand.SendState, 255, null, deviceId);

    /// <summary>Send_State, FMT 16 — строка таблицы без типизированного разбора.</summary>
    public static byte[] SendStateFmt16(uint deviceId = DEVICE_ID) =>
        Packet(MTRFXXMode.TXF, MTRFXXCommand.SendState, 16, null, deviceId);

    /// <summary>Sens_Temp_Humi от PT-111: 29,6 °C, 41 %.</summary>
    public static byte[] Microclimate() =>
        Packet(MTRFXXMode.RX, MTRFXXCommand.MicroclimateData, 7, new byte[] { 40, 33, 41, 255 }, channel: 1);

    /// <summary>
    /// Пакет без типизированного разбора (эхо команды On) с меткой в Data1 и в адресе —
    /// чтобы отличать пакеты друг от друга в проверках порядка.
    /// </summary>
    public static byte[] Untyped(byte tag = 0) =>
        Packet(MTRFXXMode.TXF, MTRFXXCommand.On, 0, new byte[] { tag, 0, 0, 0 }, tag);

    /// <summary>Пакет с испорченным стоповым маркером — Parse отвергнет.</summary>
    public static byte[] Broken()
    {
        var bytes = Untyped();
        bytes[16] = 0;
        return bytes;
    }
}

/// <summary>Ожидания с таймаутом: тест падает по таймауту, а не виснет.</summary>
public static class Wait
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Пауза для отрицательных проверок («событие НЕ пришло»): не меньше четырёх периодов
    /// опроса порта. Используется только после положительного сигнала.
    /// </summary>
    public static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(250);

    public static async Task For(Task task, string what)
    {
        try
        {
            await task.WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"timed out waiting for: {what}");
        }
    }

    public static async Task<T> For<T>(Task<T> task, string what)
    {
        try
        {
            return await task.WaitAsync(Timeout);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"timed out waiting for: {what}");
        }
    }

    public static async Task Until(Func<bool> condition, string what)
    {
        var deadline = DateTime.UtcNow + Timeout;

        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"timed out waiting for: {what}");
            await Task.Delay(10);
        }
    }
}

/// <summary>Потокобезопасный журнал событий адаптера — для проверок порядка.</summary>
public sealed class EventLog
{
    private readonly ConcurrentQueue<string> items = new();

    public void Add(string item) => items.Enqueue(item);

    public string[] Items => items.ToArray();

    public int Count => items.Count;

    public Task WaitForCount(int count) => Wait.Until(() => Count >= count, $"{count} events (got {Count})");
}

/// <summary>
/// «Медленный обработчик»: сигналит, что начался, и стоит, пока тест его не отпустит.
/// После <see cref="Release"/> все последующие вызовы проходят без задержки.
/// </summary>
public sealed class Gate
{
    private readonly ManualResetEventSlim release = new(false);
    private readonly SemaphoreSlim started = new(0);
    private int entered;

    public int Entered => Volatile.Read(ref entered);

    public void Handle(object sender, NooLite.ReceivedData data)
    {
        Interlocked.Increment(ref entered);
        started.Release();
        release.Wait();
    }

    public Task WaitStarted() => Wait.For(started.WaitAsync(), "handler start");

    public void Release() => release.Set();
}
