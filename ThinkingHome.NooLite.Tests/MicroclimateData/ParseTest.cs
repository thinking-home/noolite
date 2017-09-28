using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MicroclimateData
{
    using H = TestHelpers;
    
    public class ParseTest
    {
        [Fact]
        public void Parse_Temperature_IsCorrect()
        {
            byte[] bytes = H.GetBytes().Set(7, 70);

            var data = new NooLite.MicroclimateData(bytes);

            Assert.Equal(7, data.Temperature);
        }
    }
}