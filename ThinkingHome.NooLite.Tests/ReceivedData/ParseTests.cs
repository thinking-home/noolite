using System;
using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests.ReceivedData;

using H = TestHelpers;

public class ParseTests
{
    [Fact]
    public void Parse_Mode_IsCorrect()
    {
        const byte RXF_CODE = 3;
        var bytes = H.GetBytes().Set(1, RXF_CODE);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(MTRFXXMode.RXF, data.Mode);
    }

    [Fact]
    public void Parse_ResultCode_IsCorrect()
    {
        const byte NO_RESPONSE_CODE = 1;
        var bytes = H.GetBytes().Set(2, NO_RESPONSE_CODE);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(ResultCode.NoResponse, data.Result);
    }

    [Fact]
    public void Parse_Remains_IsCorrect()
    {
        const byte REMAINS_TEST_VALUE = 133;
        var bytes = H.GetBytes().Set(3, REMAINS_TEST_VALUE);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(REMAINS_TEST_VALUE, data.Remains);
    }

    [Fact]
    public void Parse_Channel_IsCorrect()
    {
        const byte CHANNEL_TEST_VALUE = 8;
        var bytes = H.GetBytes().Set(4, CHANNEL_TEST_VALUE);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(CHANNEL_TEST_VALUE, data.Channel);
    }

    [Fact]
    public void Parse_Command_IsCorrect()
    {
        const byte COMMAND_SEND_STATE_CODE = 130;
        var bytes = H.GetBytes().Set(5, COMMAND_SEND_STATE_CODE);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(MTRFXXCommand.SendState, data.Command);
    }

    [Fact]
    public void Parse_Data_IsCorrect()
    {
        const byte FMT_TEST_VALUE = 2;
        byte[] TEST_DATA = { 22, 33, 44, 55 };

        var bytes = H.GetBytes()
            .Set(6, FMT_TEST_VALUE)
            .Set(7, TEST_DATA);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(FMT_TEST_VALUE, data.DataFormat);
        Assert.Equal(22, data.Data1);
        Assert.Equal(33, data.Data2);
        Assert.Equal(44, data.Data3);
        Assert.Equal(55, data.Data4);
    }

    [Fact]
    public void Parse_DeviceId_IsCorrect()
    {
        byte[] bytesOfId = { 0, 21, 5, 13 };
        var bytes = H.GetBytes().Set(11, bytesOfId);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal((uint)1377549, data.DeviceId);
    }
}
