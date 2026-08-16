namespace ThinkingHome.NooLite;

/// <summary>
/// Основная информация о состоянии силового блока nooLite-F — ответ Send_State (команда 130)
/// с форматом данных 0. Создаётся только для этого формата, поэтому все свойства осмысленны.
/// Тип устройства и уровень мощности отдаются как есть, без интерпретации: реальные блоки
/// возвращают значения, отличные от документированных.
/// </summary>
public class PowerUnitStateData : ReceivedData
{
    public const byte MAIN_INFO_FORMAT = 0;

    public PowerUnitStateData(byte[] data) : base(data)
    {
    }

    /// <summary>Тип устройства (байт данных 0). Числовой код, без интерпретации.</summary>
    public byte DeviceType => Data1;

    /// <summary>Версия прошивки блока (байт данных 1).</summary>
    public byte FirmwareVersion => Data2;

    /// <summary>Состояние блока (биты 1–0 байта данных 2).</summary>
    public PowerUnitState State => (PowerUnitState)(Data3 & 0b11);

    /// <summary>Блок находится в сервисном режиме (бит 7 байта данных 2).</summary>
    public bool ServiceMode => (Data3 & 0b1000_0000) != 0;

    /// <summary>Уровень мощности / яркости (байт данных 3). Числовое значение, без интерпретации.</summary>
    public byte PowerLevel => Data4;

    public override string ToString()
    {
        return $"{base.ToString()}, device type: {DeviceType}, firmware: {FirmwareVersion}, " +
               $"state: {State}, service mode: {ServiceMode}, power level: {PowerLevel}";
    }
}
