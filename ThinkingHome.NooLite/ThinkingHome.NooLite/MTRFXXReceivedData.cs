using System;
using System.Linq;

namespace ThinkingHome.NooLite
{
    public class MTRFXXReceivedData
    {
        private readonly byte[] data;
        
        private const byte START_MARKER = 173;
        
        private const byte STOP_MARKER = 174;

        private const int BUFFER_SIZE = 17;
        
        public MTRFXXMode Mode => (MTRFXXMode) data[1];
        
        public MTRFXXCommandResult Result => (MTRFXXCommandResult) data[2];

        public int Remains => Mode == MTRFXXMode.RX | Mode == MTRFXXMode.RXF ? 0 : data[3];
        
        public int Channel => data[4];

        public MTRFXXCommand Command => (MTRFXXCommand) data[5];

        public byte DataFormat => data[6];
        
        public byte Data1 => data[7];
        public byte Data2 => data[8];
        public byte Data3 => data[9];
        public byte Data4 => data[10];

        public UInt32 DeviceId => BitConverter.ToUInt32(data, 11);

        public MTRFXXReceivedData(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != BUFFER_SIZE) throw new ArgumentException("Invalid buffer length", nameof(bytes));
            if (bytes.First() != START_MARKER) throw new ArgumentException("Invalid start marker", nameof(bytes));
            if (bytes.Last() != STOP_MARKER) throw new ArgumentException("Invalid stop marker", nameof(bytes));

            data = bytes;
        }

        public override string ToString()
        {
            return $"{{ mode: {Mode}, command: {Command}, result: {Result}, channel: {Channel}, remains: {Remains} }}";
        }

        public static MTRFXXReceivedData Parse(byte[] data)
        {
            return new MTRFXXReceivedData(data);
        }
    }
}