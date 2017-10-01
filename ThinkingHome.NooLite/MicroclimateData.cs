using System;

namespace ThinkingHome.NooLite
{
    public class MicroclimateData : ReceivedData
    {
        public MicroclimateData(byte[] data) : base(data)
        {
        }

        public decimal Temperature => ParseTemperature(Data1, Data2);

        public int? Humidity => ParseHumidity(Data2, Data3);

        public bool LowBattery => Data2 >> 7 == 1;

        private static decimal ParseTemperature(byte data1, byte data2)
        {
            int value = ((data2 & 0x0F) << 8) + data1;

            if (value >= 0x800)
            {
                value -= 0x1000;
            }

            return ((decimal)value) / 10;
        }

        private static int? ParseHumidity(byte data2, byte data3)
        {
            var type = (data2 >> 4) & 0b111;

            // sensor type: 1 - PT112, 2 - PT111
            return type == 2 ? (int?)data3 : null;
        }

        public override string ToString()
        {
            return $"{base.ToString()}, temperature: {Temperature}, humidity: {Humidity}, low battery: {LowBattery}";
        }
    }
}