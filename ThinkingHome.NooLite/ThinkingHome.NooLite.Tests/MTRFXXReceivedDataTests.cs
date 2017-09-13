using System;
using Xunit;

namespace ThinkingHome.NooLite.Tests
{
    public static class TestHelpers
    {
        public static byte[] Set(this byte[] array, int index, params byte[] values)
        {
            Array.Copy(values, 0, array, index, values.Length);
            return array;
        }
    }
    
    public class MTRFXXReceivedDataTests
    {
        private byte[] GetBytes()
        {
            byte[] bytes = new byte[17];
            bytes[0] = 173;
            bytes[16] = 174;

            return bytes;
        }

        [Fact]
        public void ReceivedData_ParseMode_IsCorrect()
        {
            byte RXF_CODE = 3;
            byte[] bytes = GetBytes().Set(1, RXF_CODE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(MTRFXXMode.RXF, data.Mode);
        }

        [Fact]
        public void ReceivedData_ParseResultCode_IsCorrect()
        {
            byte NO_RESPONSE_CODE = 1;
            byte[] bytes = GetBytes().Set(2, NO_RESPONSE_CODE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(MTRFXXCommandResult.NoResponse, data.Result);
        }
    }
}