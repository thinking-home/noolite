using System;
using System.Linq;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite
{
    public class ReceivedData
    {
        #region static

        public const byte START_MARKER = 173;
        
        public const byte STOP_MARKER = 174;

        private const int BUFFER_SIZE = 17;

        private static UInt32 ParseDeviceId(byte[] data)
        {
            UInt32 res = data[11];
            res = (res << 8) + data[12];
            res = (res << 8) + data[13];
            res = (res << 8) + data[14];

            return res;
        }
        
        #endregion
        
        #region fields

        private readonly byte[] data;
        
        public MTRFXXMode Mode => (MTRFXXMode) data[1];
        
        public ResultCode Result => (ResultCode) data[2];

        public int Remains => Mode == MTRFXXMode.RX | Mode == MTRFXXMode.RXF ? 0 : data[3];
        
        public int Channel => data[4];

        public MTRFXXCommand Command => (MTRFXXCommand) data[5];

        public byte DataFormat => data[6];
        
        public byte Data1 => data[7];
        public byte Data2 => data[8];
        public byte Data3 => data[9];
        public byte Data4 => data[10];

        public UInt32 DeviceId => ParseDeviceId(data);
        
        #endregion
        
        public ReceivedData(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length != BUFFER_SIZE) throw new ArgumentException("Invalid buffer length", nameof(data));
            if (data.First() != START_MARKER) throw new ArgumentException("Invalid start marker", nameof(data));
            if (data.Last() != STOP_MARKER) throw new ArgumentException("Invalid stop marker", nameof(data));

            this.data = (byte[])data.Clone();;
        }

        public override string ToString()
        {
            return $"mode: {Mode}, command: {Command}, result: {Result}, channel: {Channel}, remains: {Remains} " +
                   $"fmt: {DataFormat}, data: [{Data1}, {Data2}, {Data3}, {Data4}], device ID: {DeviceId}";
        }

        public static ReceivedData Parse(byte[] data)
        {
            return new ReceivedData(data);
        }
    }
}
