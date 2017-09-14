using System;
using System.Xml;
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
            const byte RXF_CODE = 3;
            byte[] bytes = GetBytes().Set(1, RXF_CODE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(MTRFXXMode.RXF, data.Mode);
        }

        [Fact]
        public void ReceivedData_ParseResultCode_IsCorrect()
        {
            const byte NO_RESPONSE_CODE = 1;
            byte[] bytes = GetBytes().Set(2, NO_RESPONSE_CODE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(MTRFXXCommandResult.NoResponse, data.Result);
        }

        [Fact]
        public void ReceivedData_ParseRemains_IsCorrect()
        {
            const byte REMAINS_TEST_VALUE = 133;
            byte[] bytes = GetBytes().Set(3, REMAINS_TEST_VALUE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(REMAINS_TEST_VALUE, data.Remains);
        }

        [Fact]
        public void ReceivedData_ParseChannel_IsCorrect()
        {
            const byte CHANNEL_TEST_VALUE = 8;
            byte[] bytes = GetBytes().Set(4, CHANNEL_TEST_VALUE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(CHANNEL_TEST_VALUE, data.Channel);
        }

        [Fact]
        public void ReceivedData_ParseCommand_IsCorrect()
        {
            const byte COMMAND_SEND_STATE_CODE = 130;
            byte[] bytes = GetBytes().Set(5, COMMAND_SEND_STATE_CODE);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(MTRFXXCommand.Send_State, data.Command);
        }

        [Fact]
        public void ReceivedData_ParseData_IsCorrect()
        {
            const byte FMT_TEST_VALUE = 2; 
            byte[] TEST_DATA = { 22, 33, 44, 55 }; 
            
            byte[] bytes = GetBytes()
                .Set(6, FMT_TEST_VALUE)
                .Set(7, TEST_DATA);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal(FMT_TEST_VALUE, data.DataFormat);
            Assert.Equal(22, data.Data1);
            Assert.Equal(33, data.Data2);
            Assert.Equal(44, data.Data3);
            Assert.Equal(55, data.Data4);
        }
        
        [Fact]
        public void ReceivedData_ParseDeviceId_IsCorrect()
        {
            byte[] bytesOfId = { 0, 21, 5, 13 };
            byte[] bytes = GetBytes().Set(11, bytesOfId);
            
            var data = new MTRFXXReceivedData(bytes);
            
            Assert.Equal((UInt32)1377549, data.DeviceId);
        }

    }
}