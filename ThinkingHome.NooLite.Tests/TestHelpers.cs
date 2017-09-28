using System;

namespace ThinkingHome.NooLite.Tests
{
    public static class TestHelpers
    {
        public static byte[] Set(this byte[] array, int index, params byte[] values)
        {
            Array.Copy(values, 0, array, index, values.Length);
            return array;
        }
        
        public static byte[] GetBytes()
        {
            byte[] bytes = new byte[17];
            bytes[0] = 173;
            bytes[16] = 174;

            return bytes;
        }
    }
}