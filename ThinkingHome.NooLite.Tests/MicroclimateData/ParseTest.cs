using Xunit;

namespace ThinkingHome.NooLite.Tests.MicroclimateData;

using H = TestHelpers;

public class ParseTest
{
    [Fact]
    public void Parse_OneByteTemperature_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(7, 0b11010111); // 215

        var data = new NooLite.MicroclimateData(bytes);

        Assert.Equal((decimal)21.5, data.Temperature);
    }

    [Fact]
    public void Parse_TwoByteTemperature_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(7, 0b00010011) // 19
            .Set(8, 0b00000001); // 1

        var data = new NooLite.MicroclimateData(bytes);

        Assert.Equal((decimal)27.5, data.Temperature);
    }

    [Fact]
    public void Parse_NegativeTemperature_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(7, 0b10011011) // 155
            .Set(8, 0b00001111); // 15

        var data = new NooLite.MicroclimateData(bytes);

        Assert.Equal((decimal)-10.1, data.Temperature);
    }

    [Fact]
    public void Parse_Humidity_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(8, 0b00100000) // bits 4-6 == 010 => PT111
            .Set(9, 0b00001101); // 13 - humidity value

        var data = new NooLite.MicroclimateData(bytes);

        Assert.Equal(13, data.Humidity);
    }

    [Fact]
    public void Parse_EmptyHumidity_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(8, 0b00010000); // bits 4-6 == 001 => PT112

        var data = new NooLite.MicroclimateData(bytes);

        Assert.Null(data.Humidity);
    }

    [Fact]
    public void Parse_PositiveLowBatteryStatus_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(8, 0b00000000); // bits 7 == 0 => battery is ok

        var data = new NooLite.MicroclimateData(bytes);

        Assert.False(data.LowBattery);
    }

    [Fact]
    public void Parse_NegativeLowBatteryStatus_IsCorrect()
    {
        var bytes = H.GetBytes()
            .Set(8, 0b10000000); // bits 7 == 1 => low battery

        var data = new NooLite.MicroclimateData(bytes);

        Assert.True(data.LowBattery);
    }
}
