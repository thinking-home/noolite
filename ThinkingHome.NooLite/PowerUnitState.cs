namespace ThinkingHome.NooLite;

/// <summary>
/// Состояние силового блока nooLite-F из ответа Send_State (биты 1–0 байта данных 2).
/// Значение 3 руководством не описано; при его получении enum примет неименованное значение.
/// </summary>
public enum PowerUnitState
{
    Off = 0,

    On = 1,

    TemporaryOn = 2
}
