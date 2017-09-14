using System;
using System.Threading;
using ThinkingHome.NooLite.Params;
using ThinkingHome.NooLite.Ports;

namespace ThinkingHome.NooLite
{
    public class MTRFXXAdapter : IDisposable
    {
        #region common

        private const int READING_INTERVAL = 50;
        private const int BUFFER_SIZE = 17;
        
        private readonly SerialPort device;
        private readonly Timer timer;

        public MTRFXXAdapter(string portName)
        {
            device = new SerialPort(portName, 9600);
            timer = new Timer(TimerCallback, null, Timeout.Infinite, READING_INTERVAL);
        }

        private void TimerCallback(object state)
        {
            lock (device)
            {
                var bytes = new byte[BUFFER_SIZE];

                while (device.BytesToRead >= BUFFER_SIZE)
                {
                    device.Read(bytes, 0, BUFFER_SIZE);
                    DataReceived?.Invoke(this, ReceivedData.Parse(bytes));
                }
            }
        }

        public void Open()
        {
            device.Open();
            timer.Change(0, READING_INTERVAL);
        }

        public bool IsOpened => device.IsOpen;

        public void Dispose()
        {
            device.Dispose();
            timer.Dispose();
        }

        #endregion

        #region commands

        public event Action<object, ReceivedData> DataReceived;

        public void SendCommand(MTRFXXMode mode, MTRFXXAction action, byte channel, MTRFXXCommand command,
            MTRFXXRepeatCount repeatCount = MTRFXXRepeatCount.NoRepeat, MTRFXXDataFormat format = MTRFXXDataFormat.NoData,
            byte[] data = null, UInt32 target = 0)
        {
            var cmd = BuildCommand(mode, action, repeatCount, channel, command, format, data, target);
            device.Write(cmd, 0, cmd.Length);
        }

        #endregion

        #region commands: static

        private const byte START_MARKER = 171;
        
        private const byte STOP_MARKER = 172;

        public static byte[] BuildCommand(MTRFXXMode mode, MTRFXXAction action, MTRFXXRepeatCount repeatCount, byte channel,
            MTRFXXCommand command, MTRFXXDataFormat format, byte[] data, UInt32 target = 0)
        {
            byte actionAndRepeatCount = (byte) ((byte) action | ((byte) repeatCount << 6));

            var res = new byte[]
            {
                START_MARKER, // 0: start marker
                (byte) mode, // 1: device mode
                actionAndRepeatCount, // 2: action & repeat count
                0, // 3: reserved
                channel, // 4: channel
                (byte) command, // 5: command
                (byte) format, // 6: data format
                0, 0, 0, 0, // 7..10: data
                0, 0, 0, 0, // 11..14: target device id
                0, // 15: checksum
                STOP_MARKER // 16: stop marker
            };

            for (int i = 0; i < 15; i++) res[15] += res[i];

            return res;
        }

        #endregion
    }
}