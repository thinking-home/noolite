using Xunit;

namespace ThinkingHome.NooLite.Tests.PowerUnitStateData;

using H = TestHelpers;

public class ParseTests
{
    private const byte TXF_MODE = 2;
    private const byte SEND_STATE_COMMAND = 130;

    // пакет Send_State FMT=0 от блока SUF-1-300 (ID 33347), как пришёл с живого адаптера
    private static byte[] SendStateBytes(byte d0, byte d1, byte d2, byte d3)
    {
        return H.GetBytes()
            .Set(1, TXF_MODE)
            .Set(5, SEND_STATE_COMMAND)
            .Set(6, 0)
            .Set(7, d0, d1, d2, d3)
            .Set(11, 0, 0, 130, 67); // 33347
    }

    [Fact]
    public void Parse_PowerUnitOn_IsCorrect()
    {
        // с живого SUF-1-300 после команды On: [5, 0, 1, 255]
        var data = new NooLite.PowerUnitStateData(SendStateBytes(5, 0, 1, 255));

        Assert.Equal(5, data.DeviceType);
        Assert.Equal(0, data.FirmwareVersion);
        Assert.Equal(PowerUnitState.On, data.State);
        Assert.False(data.ServiceMode);
        Assert.Equal(255, data.PowerLevel);
        Assert.Equal((uint)33347, data.DeviceId);
    }

    [Fact]
    public void Parse_PowerUnitOff_IsCorrect()
    {
        // с живого SUF-1-300 после команды Off: [5, 0, 0, 0]
        var data = new NooLite.PowerUnitStateData(SendStateBytes(5, 0, 0, 0));

        Assert.Equal(PowerUnitState.Off, data.State);
        Assert.Equal(0, data.PowerLevel);
    }

    [Fact]
    public void Parse_TemporaryOn_IsCorrect()
    {
        var data = new NooLite.PowerUnitStateData(SendStateBytes(5, 0, 0b10, 100));

        Assert.Equal(PowerUnitState.TemporaryOn, data.State);
    }

    [Fact]
    public void Parse_ServiceModeBit_IsIndependentOfState()
    {
        var data = new NooLite.PowerUnitStateData(SendStateBytes(5, 0, 0b1000_0001, 100));

        Assert.True(data.ServiceMode);
        Assert.Equal(PowerUnitState.On, data.State);
    }

    [Fact]
    public void Parse_UndocumentedStateBits_DoNotThrow()
    {
        var data = new NooLite.PowerUnitStateData(SendStateBytes(5, 0, 0b11, 100));

        // значение 3 руководством не описано - enum принимает неименованное значение
        Assert.Equal((PowerUnitState)3, data.State);
    }

    [Fact]
    public void Parse_DeviceTypeAndPowerLevel_AreRawBytes()
    {
        // справка обещает тип 9 и мощность 100 для SUF-1-300-A, железо отдаёт 5 и 255 -
        // библиотека не интерпретирует, отдаёт как есть
        var data = new NooLite.PowerUnitStateData(SendStateBytes(9, 3, 1, 100));

        Assert.Equal(9, data.DeviceType);
        Assert.Equal(3, data.FirmwareVersion);
        Assert.Equal(100, data.PowerLevel);
    }
}
