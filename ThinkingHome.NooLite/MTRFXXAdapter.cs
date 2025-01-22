using System;
using System.IO.Ports;
using System.Threading;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite;

public class MTRFXXAdapter : IDisposable
{
    #region events

    public event Action<object, ReceivedData> ReceiveData;
    public event Action<object, MicroclimateData> ReceiveMicroclimateData;
    public event Action<object> Connect;
    public event Action<object> Disconnect;
    public event Action<object, Exception> Error;

    #endregion

    #region common

    private readonly object lockObject = new();

    private const int READING_INTERVAL = 50;
    private const int BUFFER_SIZE = 17;

    private readonly SerialPort device;
    private readonly Timer timer;

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
                        Error?.Invoke(this, ex);
                    }
            }
    }

    public MTRFXXAdapter(string portName)
    {
        device = new SerialPort(portName, 9600);
        timer = new Timer(TimerCallback, null, Timeout.Infinite, READING_INTERVAL);
    }

    private void TimerCallback(object state)
    {
        void TryRead()
        {
            var bytes = new byte[BUFFER_SIZE];

            if (device.BytesToRead < 0) throw new Exception("adapter disconnected");

            while (device.BytesToRead >= BUFFER_SIZE)
                if (device.ReadByte() == ReceivedData.START_MARKER)
                {
                    bytes[0] = ReceivedData.START_MARKER;
                    device.Read(bytes, 1, BUFFER_SIZE - 1);

                    var data = ReceivedData.Parse(bytes);
                    ReceiveData?.Invoke(this, data);

                    if (data.Command == MTRFXXCommand.MicroclimateData &&
                        data.DataFormat == (byte)MTRFXXDataFormat.MicroclimateData)
                    {
                        var microclimateData = new MicroclimateData(bytes);
                        ReceiveMicroclimateData?.Invoke(this, microclimateData);
                    }
                }
        }

        ThreadSafeExec(true, TryRead, Close);
    }

    public bool IsOpened => device.IsOpen;

    public void Open()
    {
        ThreadSafeExec(false, () =>
        {
            device.Open();
            timer.Change(0, READING_INTERVAL);
            Connect?.Invoke(this);
        });
    }

    public void Close()
    {
        ThreadSafeExec(true, () =>
        {
            timer.Change(Timeout.Infinite, READING_INTERVAL);
            device.Close();
            Disconnect?.Invoke(this);
        });
    }

    public void Dispose()
    {
        Close();
        timer.Dispose();
    }

    #endregion

    #region commands

    public void SendCommand(MTRFXXMode mode, MTRFXXAction action, byte channel, MTRFXXCommand command,
        MTRFXXRepeatCount repeatCount = MTRFXXRepeatCount.NoRepeat, MTRFXXDataFormat format = MTRFXXDataFormat.NoData,
        byte[] data = null, uint target = 0)
    {
        var cmd = BuildCommand(mode, action, repeatCount, channel, command, format, data, target);
        device.Write(cmd, 0, cmd.Length);
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
