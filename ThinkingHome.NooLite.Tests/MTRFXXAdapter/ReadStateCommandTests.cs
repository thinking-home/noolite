using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Пакет запроса состояния: BuildCommand с аргументами, которые ReadStateF передаёт через
/// SendData. Сама цепочка ReadStateF → SendData → GetModeAndAction → SendCommand юнит-тестом
/// не покрыта (нет шва для перехвата записи в порт) — проверяется на живом адаптере.
/// Здесь — что для заданных MODE/CTR/CMD/FMT/ID пакет собирается корректно.
/// </summary>
public class ReadStateCommandTests
{
    private const byte MODE_TXF = 2;
    private const byte CTR_SEND = 0;
    private const byte CTR_BROADCAST = 1;
    private const byte CTR_TARGETED = 8;
    private const byte CMD_READ_STATE = 128;

    [Fact]
    public void ReadState_ByChannel_UsesTxfAndPlainSend()
    {
        var packet = NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand,
            MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState, MTRFXXDataFormat.NoData, null);

        Assert.Equal(MODE_TXF, packet[1]);
        Assert.Equal(CTR_SEND, packet[2]);
        Assert.Equal(0, packet[4]);
        Assert.Equal(CMD_READ_STATE, packet[5]);
        Assert.Equal(0, packet[6]);
        Assert.Equal(new byte[] { 0, 0, 0, 0 }, packet[11..15]);
    }

    [Fact]
    public void ReadState_ByDeviceId_UsesTargetedSendWithId()
    {
        var packet = NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendTargetedCommand,
            MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState, MTRFXXDataFormat.NoData, null, 33347);

        Assert.Equal(CTR_TARGETED, packet[2]);
        Assert.Equal(new byte[] { 0, 0, 130, 67 }, packet[11..15]);
    }

    [Fact]
    public void ReadState_WithFormat_PutsRowAddressIntoFmtByte()
    {
        var packet = NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand,
            MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState, (MTRFXXDataFormat)16, null);

        Assert.Equal(16, packet[6]);
    }

    [Fact]
    public void ReadState_Broadcast_UsesBroadcastCtr()
    {
        var packet = NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendBroadcastCommand,
            MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState, MTRFXXDataFormat.NoData, null);

        Assert.Equal(CTR_BROADCAST, packet[2]);
    }
}
