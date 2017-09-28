using Xunit;

namespace ThinkingHome.NooLite.Tests.MicroclimateData
{
    using H = TestHelpers;
    
    public class ParseTest
    {
        [Fact]
        public void Parse_OneByteTemperature_IsCorrect()
        {
            byte[] bytes = H.GetBytes()
                .Set(7, 215);

            var data = new NooLite.MicroclimateData(bytes);

            Assert.Equal((decimal)21.5, data.Temperature);
        }

        [Fact]
        public void Parse_TwoByteTemperature_IsCorrect()
        {
            byte[] bytes = H.GetBytes()
                .Set(7, 19)
                .Set(8, 1);

            var data = new NooLite.MicroclimateData(bytes);

            Assert.Equal((decimal)27.5, data.Temperature);
        }
        
        [Fact]
        public void Parse_NegativeTemperature_IsCorrect()
        {
            byte[] bytes = H.GetBytes()
                .Set(7, 155)
                .Set(8, 15);

            var data = new NooLite.MicroclimateData(bytes);

            Assert.Equal((decimal)-10.1, data.Temperature);
        }
    }
}