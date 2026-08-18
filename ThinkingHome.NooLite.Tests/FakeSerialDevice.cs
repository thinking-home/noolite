using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite.Tests;

/// <summary>
/// Подставной порт для тестов адаптера. Входящие байты подкладываются через <see cref="Feed"/>
/// из потока теста и вычитываются потоком таймера адаптера; исходящие копятся в общем журнале.
/// </summary>
public sealed class FakeSerialDevice : ISerialDevice
{
    private readonly object sync = new();

    // входящие: очередь байтов, которую вычитывает адаптер
    private readonly Queue<byte> incoming = new();

    // исходящие: всё, что адаптер записал, байт за байтом
    private readonly List<byte> written = new();

    private volatile bool isOpen;

    public bool IsOpen => isOpen;

    /// <summary>Следующее обращение к <see cref="BytesToRead"/> бросит это исключение (одноразово).</summary>
    public Exception FailRead { get; set; }

    /// <summary>Каждый <see cref="Write"/> бросает это исключение, пока свойство не сброшено.</summary>
    public Exception FailWrite { get; set; }

    public int OpenCount { get; private set; }

    public int CloseCount { get; private set; }

    public int BytesToRead
    {
        get
        {
            var fail = FailRead;

            if (fail != null)
            {
                FailRead = null;
                throw fail;
            }

            lock (sync) return incoming.Count;
        }
    }

    public void Open()
    {
        isOpen = true;
        OpenCount++;
    }

    public void Close()
    {
        isOpen = false;
        CloseCount++;
    }

    public int ReadByte()
    {
        lock (sync) return incoming.Dequeue();
    }

    public int Read(byte[] buffer, int offset, int count)
    {
        lock (sync)
        {
            for (var i = 0; i < count; i++) buffer[offset + i] = incoming.Dequeue();
        }

        return count;
    }

    public void Write(byte[] buffer, int offset, int count)
    {
        // как SerialPort: запись в закрытый порт - InvalidOperationException
        if (!isOpen) throw new InvalidOperationException("The port is closed.");

        var fail = FailWrite;
        if (fail != null) throw fail;

        // байты копируются по одному с уступкой планировщику между ними: окно гонки между
        // параллельными Write расширяется с наносекунд до величины, которую тест ловит надёжно
        for (var i = 0; i < count; i++)
        {
            lock (sync) written.Add(buffer[offset + i]);
            Thread.Yield();
        }
    }

    /// <summary>Подложить пакеты (или произвольные байты) во входящий буфер.</summary>
    public void Feed(params byte[][] packets)
    {
        lock (sync)
        {
            foreach (var packet in packets)
            foreach (var b in packet)
                incoming.Enqueue(b);
        }
    }

    /// <summary>Снимок журнала записанных байтов.</summary>
    public byte[] Written
    {
        get
        {
            lock (sync) return written.ToArray();
        }
    }

    /// <summary>Журнал записанных байтов, нарезанный на пакеты по 17 байт.</summary>
    public byte[][] WrittenPackets => Written.Chunk(17).ToArray();
}
