using System;

namespace ThinkingHome.NooLite
{
    public class MicroclimateData : ReceivedData
    {
        public MicroclimateData(byte[] data) : base(data)
        {
        }
        
        public decimal Temperature => ParseTemperature(Data1, Data2);

        public int Humidity => Data3;
        
        public static decimal ParseTemperature(byte data1, byte data2)
        {
            int value = ((data2 & 0x0F) << 8) + data1;

            if (value >= 0x800)
            {
                value -= 0x1000;
            }

            return ((decimal)value) / 10;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, temperature: {Temperature}, humidity: {Humidity}";
        }
    }
}