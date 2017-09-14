using System;
using Xunit;

namespace ThinkingHome.NooLite.Tests.ReceivedData
{
    public class ValidationTests
    {
        public const int VALID_DATA_SIZE = 17;
        
        [Fact]
        public void Constructor_ForNull_ThrowsException()
        {
            byte[] nullReceivedData = null;

            Exception ex = Assert.ThrowsAny<Exception>(() => new MTRFXXReceivedData(nullReceivedData));
            
            Assert.Contains("null", ex.Message);
        }

        [Fact]
        public void Constructor_ForInvalidDataSize_ThrowsException()
        {
            byte[] invalidSizeData = new byte[3];

            Exception ex = Assert.ThrowsAny<Exception>(() => new MTRFXXReceivedData(invalidSizeData));
            
            Assert.Contains("length", ex.Message);
        }

        
        [Fact]
        public void Constructor_ForInvalidFirstByte_ThrowsException()
        {
            byte[] testData = new byte[VALID_DATA_SIZE];
            testData[0] = 124;

            Exception ex = Assert.ThrowsAny<Exception>(() => new MTRFXXReceivedData(testData));
            
            Assert.Contains("start", ex.Message);
        }

        [Fact]
        public void Constructor_ForInvalidLastByte_ThrowsException()
        {
            byte[] testData = new byte[VALID_DATA_SIZE];
            testData[0] = 173;    // valid start marker
            testData[VALID_DATA_SIZE - 1] = 124;

            Exception ex = Assert.ThrowsAny<Exception>(() => new MTRFXXReceivedData(testData));
            
            Assert.Contains("stop", ex.Message);
        }
    }
}