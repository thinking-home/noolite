using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite;

public class MTRFXXAdapter : IDisposable
{
    #region events

    public event Action<object, ReceivedData> ReceiveData;
    public event Action<object, MicroclimateData> ReceiveMicroclimateData;

    /// <summary>
    /// Пришла основная информация о состоянии блока nooLite-F (Send_State, FMT=0).
    /// Срабатывает не только в ответ на <c>ReadStateF</c>: блок присылает своё состояние
    /// после любой команды в режиме TXF (On, Off, Bind и т.д.).
    /// </summary>
    public event Action<object, PowerUnitStateData> ReceivePowerUnitState;

    /// <summary>
    /// Блок nooLite-F ответил ошибкой формата на запрос состояния (Send_State, FMT=255) —
    /// запрошена несуществующая строка таблицы состояния.
    /// </summary>
    public event Action<object, StateFormatErrorData> ReceiveStateFormatError;

    public event Action<object> Connect;
    public event Action<object> Disconnect;
    public event Action<object, Exception> Error;

    #endregion

    #region common

    private readonly object lockObject = new();

    private const int READING_INTERVAL = 50;
    private const int BUFFER_SIZE = 17;

    public const int DEFAULT_QUEUE_CAPACITY = 128;

    private readonly ISerialDevice device;
    private readonly Timer timer;

    // принятые из порта пакеты; поток таймера только кладёт, диспетчер вынимает и разбирает
    private readonly Channel<byte[]> queue;

    private int droppedPackets;

    // выставляется Close/FlushAndCloseAsync: диспетчер отбрасывает всё, что вынет после этого,
    // чтобы ни один обработчик не был вызван после Disconnect
    private volatile bool closing;

    // стоит, пока выполняются обработчики событий пакета; течёт через await/Task.Run —
    // так FlushAndCloseAsync узнаёт, что его позвали изнутри обработчика
    private static readonly AsyncLocal<bool> insideHandler = new();

    // FlushAndCloseAsync ждёт этот сигнал; диспетчер завершает его, когда очередь опустела
    // и обработчики последнего вынутого пакета отработали
    private volatile TaskCompletionSource drained;

    // 1, пока диспетчер вынул пакет и ещё не закончил его обработчики: без этого
    // FlushAndCloseAsync мог бы увидеть пустую очередь и вернуться посреди обработчика
    private int inFlight;

    private void ThreadSafeExec(bool isOpen, Action fn, Action errorHandler = null)
    {
        if (device.IsOpen == isOpen)
            lock (lockObject)
            {
                if (device.IsOpen == isOpen)
                    try
                    {
                        fn();
                    }
                    catch (Exception ex)
                    {
                        errorHandler?.Invoke();
                        RaiseError(ex);
                    }
            }
    }

    /// <param name="portName">Имя последовательного порта адаптера (9600 бод, таймаут записи 500 мс).</param>
    /// <param name="queueCapacity">
    /// Ёмкость очереди принятых пакетов. При переполнении новый пакет отбрасывается, очередь
    /// сохраняет уже принятые в порядке прихода; число отброшенных доступно через
    /// <see cref="DroppedPacketsCount"/>.
    /// </param>
    public MTRFXXAdapter(string portName, int queueCapacity = DEFAULT_QUEUE_CAPACITY)
        : this(new SerialPortDevice(portName), queueCapacity)
    {
    }

    // шов для тестов: подставной порт вместо SerialPort. поведение адаптера от реализации
    // порта не зависит - он использует только члены ISerialDevice
    internal MTRFXXAdapter(ISerialDevice device, int queueCapacity = DEFAULT_QUEUE_CAPACITY)
    {
        ArgumentNullException.ThrowIfNull(device);

        if (queueCapacity < 1)
            throw new ArgumentOutOfRangeException(nameof(queueCapacity), queueCapacity,
                "queue capacity must be positive");

        this.device = device;
        timer = new Timer(TimerCallback, null, Timeout.Infinite, READING_INTERVAL);

        // при переполнении отбрасывается НОВЫЙ пакет (DropWrite): очередь отдаёт пакеты
        // в порядке прихода, и вытеснять уже принятые ради новых нечестно - в очереди могут
        // лежать пакеты от разных устройств, новый не "обновляет" старый, а теряет чужое событие
        queue = Channel.CreateBounded<byte[]>(
            new BoundedChannelOptions(queueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            },
            _ => Interlocked.Increment(ref droppedPackets));

        // один диспетчер на весь жизненный цикл адаптера; Open/Close управляют портом
        // и таймером, но не им - он завершается только вместе с каналом в Dispose
        Task.Run(DispatchAsync);
    }

    /// <summary>
    /// Число пакетов, отброшенных из-за переполнения очереди принятых пакетов.
    /// </summary>
    public int DroppedPacketsCount => Volatile.Read(ref droppedPackets);

    private void TimerCallback(object state)
    {
        // под замком - только чтение из порта и постановка в очередь; разбор и события -
        // в диспетчере, чтобы обработчики потребителя не держали замок
        void TryRead()
        {
            if (device.BytesToRead < 0) throw new Exception("adapter disconnected");

            while (device.BytesToRead >= BUFFER_SIZE)
                if (device.ReadByte() == ReceivedData.START_MARKER)
                {
                    // на каждый пакет свой массив: он уходит в очередь и живёт дольше этого цикла
                    var bytes = new byte[BUFFER_SIZE];
                    bytes[0] = ReceivedData.START_MARKER;
                    device.Read(bytes, 1, BUFFER_SIZE - 1);

                    queue.Writer.TryWrite(bytes);
                }
        }

        ThreadSafeExec(true, TryRead, Close);
    }

    private async Task DispatchAsync()
    {
        var reader = queue.Reader;

        while (await reader.WaitToReadAsync().ConfigureAwait(false))
        {
            // inFlight поднимается ДО вынимания: иначе между TryRead (Count стал 0)
            // и inFlight=1 FlushAndCloseAsync увидел бы "пусто и никто не занят"
            Volatile.Write(ref inFlight, 1);

            try
            {
                while (reader.TryRead(out var bytes))
                {
                    // между выниманием и вызовом обработчиков мог случиться Close: тогда пакет
                    // отбрасывается, чтобы после Disconnect не было ни одного события
                    if (!closing) Dispatch(bytes);
                }
            }
            finally
            {
                Volatile.Write(ref inFlight, 0);
            }

            // очередь вычитана до дна и обработчики отработали - сигнал FlushAndCloseAsync
            drained?.TrySetResult();
        }
    }

    private void Dispatch(byte[] bytes)
    {
        ReceivedData data;

        try
        {
            data = ReceivedData.Parse(bytes);
        }
        catch (Exception ex)
        {
            RaiseError(ex);
            return;
        }

        insideHandler.Value = true;

        try
        {
            Raise(ReceiveData, data);

            if (data.Command == MTRFXXCommand.MicroclimateData &&
                data.DataFormat == (byte)MTRFXXDataFormat.MicroclimateData)
            {
                Raise(ReceiveMicroclimateData, new MicroclimateData(bytes));
            }
            else if (data.Command == MTRFXXCommand.SendState)
            {
                // типизируются только известные форматы; прочие строки таблицы
                // состояния доходят до потребителя лишь через ReceiveData
                if (data.DataFormat == PowerUnitStateData.MAIN_INFO_FORMAT)
                    Raise(ReceivePowerUnitState, new PowerUnitStateData(bytes));
                else if (data.DataFormat == StateFormatErrorData.ERROR_FORMAT)
                    Raise(ReceiveStateFormatError, new StateFormatErrorData(bytes));
            }
        }
        finally
        {
            insideHandler.Value = false;
        }
    }

    // обработчики изолированы: исключение одного уходит в Error, следующий вызывается
    private void Raise<T>(Action<object, T> handler, T arg)
    {
        try
        {
            handler?.Invoke(this, arg);
        }
        catch (Exception ex)
        {
            RaiseError(ex);
        }
    }

    private void RaiseError(Exception ex)
    {
        try
        {
            Error?.Invoke(this, ex);
        }
        catch
        {
            // обработчик Error сам бросил - глотаем, иначе один сбойный обработчик
            // остановил бы доставку всех событий
        }
    }

    public bool IsOpened => device.IsOpen;

    public void Open()
    {
        ThreadSafeExec(false, () =>
        {
            closing = false;
            device.Open();
            timer.Change(0, READING_INTERVAL);
            Connect?.Invoke(this);
        });
    }

    /// <summary>
    /// Закрыть порт немедленно. Пакеты, принятые, но ещё не доставленные обработчикам,
    /// <b>отбрасываются</b>. Чтобы дождаться их обработки, используйте
    /// <see cref="FlushAndCloseAsync"/>.
    /// </summary>
    public void Close()
    {
        ThreadSafeExec(true, () =>
        {
            closing = true;
            timer.Change(Timeout.Infinite, READING_INTERVAL);
            device.Close();
            DiscardQueue();
            Disconnect?.Invoke(this);
        });
    }

    /// <summary>
    /// Закрыть порт немедленно, но дождаться, пока все уже принятые пакеты будут доставлены
    /// обработчикам; затем вызвать <see cref="Disconnect"/>. По возврату очередь пуста.
    /// Отмена через <paramref name="cancellationToken"/> — остаток отбрасывается.
    /// Нельзя вызывать изнутри обработчика события — диспетчер ждал бы сам себя.
    /// </summary>
    public async Task FlushAndCloseAsync(CancellationToken cancellationToken = default)
    {
        if (insideHandler.Value)
            throw new InvalidOperationException(
                "FlushAndCloseAsync cannot be awaited from inside an event handler: " +
                "the dispatcher would wait for itself");

        var wait = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var wasOpen = false;

        // порт закрывается сразу - новых пакетов не будет; очередь НЕ очищается,
        // closing НЕ ставится - диспетчер должен доработать остаток
        ThreadSafeExec(true, () =>
        {
            wasOpen = true;
            timer.Change(Timeout.Infinite, READING_INTERVAL);
            device.Close();
            drained = wait;
        });

        if (!wasOpen) return;

        try
        {
            // если очередь уже пуста и диспетчер не внутри обработчиков - ждать нечего
            // (и сигнала не будет: диспетчер спит на WaitToReadAsync). иначе ждём:
            // диспетчер сигналит после обработчиков последнего пакета
            if (queue.Reader.Count > 0 || Volatile.Read(ref inFlight) != 0)
                await wait.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            DiscardQueue();
            throw;
        }
        finally
        {
            drained = null;
            closing = true;
            Disconnect?.Invoke(this);
        }
    }

    private void DiscardQueue()
    {
        while (queue.Reader.TryRead(out _))
        {
        }
    }

    public void Dispose()
    {
        Close();
        queue.Writer.TryComplete();
        timer.Dispose();
    }

    #endregion

    #region commands

    public void SendCommand(MTRFXXMode mode, MTRFXXAction action, byte channel, MTRFXXCommand command,
        MTRFXXRepeatCount repeatCount = MTRFXXRepeatCount.NoRepeat, MTRFXXDataFormat format = MTRFXXDataFormat.NoData,
        byte[] data = null, uint target = 0)
    {
        var cmd = BuildCommand(mode, action, repeatCount, channel, command, format, data, target);

        // пакет пишется под общим замком целиком: параллельные вызовы не должны
        // перемешать байты. ошибки намеренно не перехватываются - вызывающей стороне
        // нужно достоверно знать, ушла команда или нет
        lock (lockObject)
        {
            device.Write(cmd, 0, cmd.Length);
        }
    }

    #endregion

    #region commands: static

    private const byte START_MARKER = 171;

    private const byte STOP_MARKER = 172;

    public static byte[] BuildCommand(MTRFXXMode mode, MTRFXXAction action, MTRFXXRepeatCount repeatCount, byte channel,
        MTRFXXCommand command, MTRFXXDataFormat format, byte[] data, uint target = 0)
    {
        var actionAndRepeatCount = (byte)((byte)action | ((byte)repeatCount << 6));
        var id1 = (byte)(target >> 24);
        var id2 = (byte)(target >> 16);
        var id3 = (byte)(target >> 8);
        var id4 = (byte)target;

        var d = data ?? Array.Empty<byte>();

        var d1 = d.Length > 0 ? d[0] : (byte)0;
        var d2 = d.Length > 1 ? d[1] : (byte)0;
        var d3 = d.Length > 2 ? d[2] : (byte)0;
        var d4 = d.Length > 3 ? d[3] : (byte)0;

        var res = new byte[]
        {
            START_MARKER, // 0: start marker
            (byte)mode, // 1: device mode
            actionAndRepeatCount, // 2: action & repeat count
            0, // 3: reserved
            channel, // 4: channel
            (byte)command, // 5: command
            (byte)format, // 6: data format
            d1, d2, d3, d4, // 7..10: data
            id1, id2, id3, id4, // 11..14: target device id
            0, // 15: checksum
            STOP_MARKER // 16: stop marker
        };

        for (var i = 0; i < 15; i++) res[15] += res[i];

        return res;
    }

    #endregion
}
