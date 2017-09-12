using System;
using System.Linq;

namespace ThinkingHome.NooLite
{
    public class MTRFXXReceivedData
    {
        private const byte START_MARKER = 173;
        
        private const byte STOP_MARKER = 174;

        private const int BUFFER_SIZE = 17;
        
        public MTRFXXMode Mode { get; }
        
        public MTRFXXCommandResult Result { get; }
        
        public MTRFXXCommand Command { get; }
        
        public int Remains { get; }
        
        public int Channel { get; }
        
        public MTRFXXReceivedData(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            if (bytes.Length != BUFFER_SIZE) throw new ArgumentException("Invalid buffer length", nameof(bytes));
            if (bytes.First() != START_MARKER) throw new ArgumentException("Invalid start marker", nameof(bytes));
            if (bytes.Last() != STOP_MARKER) throw new ArgumentException("Invalid stop marker", nameof(bytes));

            Mode = (MTRFXXMode) bytes[1];
            Result = (MTRFXXCommandResult) bytes[2];
            Remains = Mode == MTRFXXMode.RX | Mode == MTRFXXMode.RXF ? 0 : bytes[3];
            Channel = bytes[4];
            Command = (MTRFXXCommand) bytes[5];
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