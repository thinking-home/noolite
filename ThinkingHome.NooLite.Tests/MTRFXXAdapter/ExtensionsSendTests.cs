using System;
using ThinkingHome.NooLite.Internal;
using Xunit;

namespace ThinkingHome.NooLite.Tests.MTRFXXAdapter;

/// <summary>
/// Цепочка метод-расширение → SendData/Send → GetModeAndAction → SendCommand → байты в порту.
/// В каждом тесте: открытый адаптер на подставном порту, один вызов расширения, затем сверка
/// единственного записанного пакета с эталоном <c>BuildCommand(...)</c>, собранным из ожидаемых
/// MODE / CTR / канала / CMD / FMT / ID. Так проверяется не сборка пакета (это в
/// <see cref="ReadStateCommandTests"/>), а то, что расширение передало в <c>SendCommand</c>
/// именно эти аргументы и байты дошли до порта.
/// Спека: command-sending → «Запрос состояния блока nooLite-F».
/// </summary>
[Collection(AdapterCollection.Name)]
public class ExtensionsSendTests
{
    private static (NooLite.MTRFXXAdapter adapter, FakeSerialDevice port) OpenAdapter()
    {
        var port = new FakeSerialDevice();
        var adapter = new NooLite.MTRFXXAdapter(port);
        adapter.Open();
        return (adapter, port);
    }

    private static void AssertSingleWritten(FakeSerialDevice port, byte[] expected)
    {
        var packet = Assert.Single(port.WrittenPackets);
        Assert.Equal(expected, packet);
    }

    /// <summary>
    /// Что: <c>ReadStateF(0)</c> без ID — запрос по каналу: режим TXF, обычная передача (CTR=0),
    /// команда Read_State (128), FMT 0, нулевой адрес.
    /// Спека: command-sending → «Запрос по каналу».
    /// </summary>
    [Fact]
    public void ReadStateF_ByChannel_WritesTxfPlainReadState()
    {
        var (adapter, port) = OpenAdapter();
        using (adapter)
        {
            adapter.ReadStateF(0);

            AssertSingleWritten(port, NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand,
                MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState, MTRFXXDataFormat.NoData, null));
        }
    }

    /// <summary>
    /// Что: <c>ReadStateF(0, 33347)</c> с ID блока — адресный запрос: CTR=8
    /// (<c>SendTargetedCommand</c>), адрес 33347 в байтах 11–14.
    /// Спека: command-sending → «Адресный запрос».
    /// </summary>
    [Fact]
    public void ReadStateF_ByDeviceId_WritesTargetedReadStateWithId()
    {
        var (adapter, port) = OpenAdapter();
        using (adapter)
        {
            adapter.ReadStateF(0, Packets.DEVICE_ID);

            AssertSingleWritten(port, NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF,
                MTRFXXAction.SendTargetedCommand, MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState,
                MTRFXXDataFormat.NoData, null, Packets.DEVICE_ID));
        }
    }

    /// <summary>
    /// Что: <c>ReadStateF(0, format: 16)</c> — адрес строки таблицы состояния уходит в байт FMT.
    /// Спека: command-sending → «Запрос другой строки таблицы».
    /// </summary>
    [Fact]
    public void ReadStateF_WithFormat_WritesRowAddressIntoFmt()
    {
        var (adapter, port) = OpenAdapter();
        using (adapter)
        {
            adapter.ReadStateF(0, format: 16);

            AssertSingleWritten(port, NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand,
                MTRFXXRepeatCount.NoRepeat, 0, MTRFXXCommand.ReadState, (MTRFXXDataFormat)16, null));
        }
    }

    /// <summary>
    /// Что: <c>OnF(3)</c> — команда On (2) в режиме TXF по каналу 3, без данных и адреса.
    /// Контекст: типовое расширение через <c>Send</c> (не <c>SendData</c>).
    /// </summary>
    [Fact]
    public void OnF_ByChannel_WritesTxfOn()
    {
        var (adapter, port) = OpenAdapter();
        using (adapter)
        {
            adapter.OnF(3);

            AssertSingleWritten(port, NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand,
                MTRFXXRepeatCount.NoRepeat, 3, MTRFXXCommand.On, MTRFXXDataFormat.NoData, null));
        }
    }

    /// <summary>
    /// Что: <c>Off(3)</c> — команда Off (0) в обычном режиме TX (не F) по каналу 3.
    /// Контекст: расширение без суффикса F должно выбрать режим TX, а не TXF.
    /// </summary>
    [Fact]
    public void Off_ByChannel_WritesTxOff()
    {
        var (adapter, port) = OpenAdapter();
        using (adapter)
        {
            adapter.Off(3);

            AssertSingleWritten(port, NooLite.MTRFXXAdapter.BuildCommand(MTRFXXMode.TX, MTRFXXAction.SendCommand,
                MTRFXXRepeatCount.NoRepeat, 3, MTRFXXCommand.Off, MTRFXXDataFormat.NoData, null));
        }
    }
}
