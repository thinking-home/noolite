namespace ThinkingHome.NooLite;

/// <summary>
/// Ответ блока nooLite-F на запрос несуществующей строки таблицы состояния — Send_State
/// (команда 130) с форматом данных 255. Сам объект и есть признак ошибки; канал и адрес
/// ответившего блока доступны из базового класса.
/// </summary>
public class StateFormatErrorData : ReceivedData
{
    public const byte ERROR_FORMAT = 255;

    public StateFormatErrorData(byte[] data) : base(data)
    {
    }

    public override string ToString()
    {
        return $"{base.ToString()}, state format error: unknown state table row requested";
    }
}
