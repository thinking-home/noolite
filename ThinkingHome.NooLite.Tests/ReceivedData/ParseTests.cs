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

    [Theory]
    [InlineData(0, 133)] // TX
    [InlineData(2, 133)] // TXF
    public void Parse_Togl_ForTxModes_IsRemains(byte mode, byte togl)
    {
        var bytes = H.GetBytes().Set(1, mode).Set(3, togl);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(togl, data.Togl);
        Assert.Equal((int?)togl, data.Remains);
        Assert.Null(data.ToggleCounter);
    }

    [Theory]
    [InlineData(1, 137)] // RX
    [InlineData(3, 137)] // RXF
    public void Parse_Togl_ForRxModes_IsToggleCounter(byte mode, byte togl)
    {
        var bytes = H.GetBytes().Set(1, mode).Set(3, togl);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(togl, data.Togl);
        Assert.Null(data.Remains);
        Assert.Equal((int?)togl, data.ToggleCounter);
    }

    [Theory]
    [InlineData(4)] // Service
    [InlineData(5)] // Update
    public void Parse_Togl_ForOtherModes_IsRawOnly(byte mode)
    {
        const byte TOGL_TEST_VALUE = 42;
        var bytes = H.GetBytes().Set(1, mode).Set(3, TOGL_TEST_VALUE);

        var data = new NooLite.ReceivedData(bytes);

        Assert.Equal(TOGL_TEST_VALUE, data.Togl);
        Assert.Null(data.Remains);
        Assert.Null(data.ToggleCounter);
    }

    [Fact]
    public void Parse_Togl_ForRepeatedRxPackets_IsSame()
    {
        const byte RX_MODE = 1;
        const byte TOGL_TEST_VALUE = 7;

        var first = new NooLite.ReceivedData(H.GetBytes().Set(1, RX_MODE).Set(3, TOGL_TEST_VALUE));
        var repeat = new NooLite.ReceivedData(H.GetBytes().Set(1, RX_MODE).Set(3, TOGL_TEST_VALUE));

        Assert.Equal(first.ToggleCounter, repeat.ToggleCounter);
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
