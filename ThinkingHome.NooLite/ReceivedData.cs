using System;
using System.Linq;
using ThinkingHome.NooLite.Internal;

namespace ThinkingHome.NooLite;

public class ReceivedData
{
    #region static

    public const byte START_MARKER = 173;

    public const byte STOP_MARKER = 174;

    private const int BUFFER_SIZE = 17;

    private static uint ParseDeviceId(byte[] data)
    {
        uint res = data[11];
        res = (res << 8) + data[12];
        res = (res << 8) + data[13];
        res = (res << 8) + data[14];

        return res;
    }

    #endregion

    #region fields

    private readonly byte[] data;

    public MTRFXXMode Mode => (MTRFXXMode)data[1];

    public ResultCode Result => (ResultCode)data[2];

    private bool IsTx => Mode is MTRFXXMode.TX or MTRFXXMode.TXF;

    private bool IsRx => Mode is MTRFXXMode.RX or MTRFXXMode.RXF;

    /// <summary>
    /// Сырой байт TOGL (байт 3 пакета), без интерпретации. Смысл зависит от режима:
    /// для TX/TXF см. <see cref="Remains"/>, для RX/RXF — <see cref="ToggleCounter"/>.
    /// Для сервисного режима и режима обновления ПО семантика байта руководством не описана —
    /// это единственный способ его прочитать.
    /// </summary>
    public byte Togl => data[3];

    /// <summary>
    /// Сколько пакетов ответа адаптер ещё передаст после этого (последний пакет серии — 0).
    /// Осмысленно только для режимов TX и TXF; для остальных — <c>null</c>.
    /// </summary>
    public int? Remains => IsTx ? data[3] : null;

    /// <summary>
    /// Счётчик команд передатчика: увеличивается на единицу при каждой новой команде,
    /// у повторов одной посылки значение одинаковое — по нему отличают новое событие
    /// от повтора. Осмысленно только для режимов RX и RXF; для остальных — <c>null</c>.
    /// </summary>
    public int? ToggleCounter => IsRx ? data[3] : null;

    public int Channel => data[4];

    public MTRFXXCommand Command => (MTRFXXCommand)data[5];

    public byte DataFormat => data[6];

    public byte Data1 => data[7];
    public byte Data2 => data[8];
    public byte Data3 => data[9];
    public byte Data4 => data[10];

    public uint DeviceId => ParseDeviceId(data);

    #endregion

    public ReceivedData(byte[] data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (data.Length != BUFFER_SIZE) throw new ArgumentException("Invalid buffer length", nameof(data));
        if (data.First() != START_MARKER) throw new ArgumentException("Invalid start marker", nameof(data));
        if (data.Last() != STOP_MARKER) throw new ArgumentException("Invalid stop marker", nameof(data));

        this.data = (byte[])data.Clone();
        ;
    }

    public override string ToString()
    {
        var togl = IsTx ? $", remains: {Remains}"
            : IsRx ? $", toggle: {ToggleCounter}"
            : string.Empty;

        return $"mode: {Mode}, command: {Command}, result: {Result}, channel: {Channel}{togl}, " +
               $"fmt: {DataFormat}, data: [{Data1}, {Data2}, {Data3}, {Data4}], device ID: {DeviceId}";
    }

    public static ReceivedData Parse(byte[] data)
    {
        return new ReceivedData(data);
    }
}
