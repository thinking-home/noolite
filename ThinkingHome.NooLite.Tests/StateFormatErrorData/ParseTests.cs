using Xunit;

namespace ThinkingHome.NooLite.Tests.StateFormatErrorData;

using H = TestHelpers;

public class ParseTests
{
    private const byte TXF_MODE = 2;
    private const byte SEND_STATE_COMMAND = 130;
    private const byte ERROR_FORMAT = 255;

    [Fact]
    public void Parse_FormatError_ExposesBaseFields()
    {
        var bytes = H.GetBytes()
            .Set(1, TXF_MODE)
            .Set(4, 7)
            .Set(5, SEND_STATE_COMMAND)
            .Set(6, ERROR_FORMAT)
            .Set(11, 0, 0, 130, 67); // 33347

        var data = new NooLite.StateFormatErrorData(bytes);

        Assert.Equal(ERROR_FORMAT, data.DataFormat);
        Assert.Equal(7, data.Channel);
        Assert.Equal((uint)33347, data.DeviceId);
        Assert.Contains("format error", data.ToString());
    }
}
