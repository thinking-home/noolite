using System;
using System.Linq;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite
{
    public class ReceivedData
    {
        #region static

        private const byte START_MARKER = 173;
        
        private const byte STOP_MARKER = 174;

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
        
        public readonly MTRFXXMode Mode;
        
        public readonly ResultCode Result;

        public readonly int Remains;
        
        public readonly int Channel;

        public readonly MTRFXXCommand Command;

        public readonly byte DataFormat;
        
        public readonly byte Data1;
        public readonly byte Data2;
        public readonly byte Data3;
        public readonly byte Data4;

        public readonly UInt32 DeviceId;
        
        #endregion
        
        public ReceivedData(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (data.Length != BUFFER_SIZE) throw new ArgumentException("Invalid buffer length", nameof(data));
            if (data.First() != START_MARKER) throw new ArgumentException("Invalid start marker", nameof(data));
            if (data.Last() != STOP_MARKER) throw new ArgumentException("Invalid stop marker", nameof(data));
            
            Mode = (MTRFXXMode) data[1];
            Result = (ResultCode) data[2];
            Remains = Mode == MTRFXXMode.RX | Mode == MTRFXXMode.RXF ? 0 : data[3];
            Channel = data[4];
            Command = (MTRFXXCommand) data[5];

            DataFormat = data[6];
            Data1 = data[7];
            Data2 = data[8];
            Data3 = data[9];
            Data4 = data[10];

            DeviceId = ParseDeviceId(data);
        }

        public override string ToString()
        {
            return $"{{ " +
                   $"mode: {Mode}, command: {Command}, result: {Result}, channel: {Channel}, remains: {Remains} " +
                   $"fmt: {DataFormat}, data: [{Data1}, {Data2}, {Data3}, {Data4}], device ID: {DeviceId} " +
                   $"}}";
        }

        public static ReceivedData Parse(byte[] data)
        {
            return new ReceivedData(data);
        }
    }
}