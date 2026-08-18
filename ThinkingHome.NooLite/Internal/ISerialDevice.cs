namespace ThinkingHome.NooLite.Internal;

/// <summary>
/// Порт, через который адаптер обменивается байтами с MTRF-64. Ровно те члены
/// <see cref="System.IO.Ports.SerialPort"/>, которые использует <see cref="MTRFXXAdapter"/>;
/// нужен, чтобы подставить порт в тестах.
/// </summary>
internal interface ISerialDevice
{
    bool IsOpen { get; }

    /// <summary>Число байтов, доступных для чтения. Может бросить, если порт отвалился.</summary>
    int BytesToRead { get; }

    void Open();

    void Close();

    int ReadByte();

    /// <summary>
    /// Адаптер вызывает только тогда, когда <see cref="BytesToRead"/> не меньше <paramref name="count"/>,
    /// и рассчитывает получить ровно <paramref name="count"/> байтов.
    /// </summary>
    int Read(byte[] buffer, int offset, int count);

    /// <summary>
    /// Запись в закрытый порт бросает исключение — на этом держится «отправка при закрытом
    /// порту → исключение вызывающему». Запись должна быть ограничена во времени: она идёт
    /// под тем же замком, что и чтение из порта.
    /// </summary>
    void Write(byte[] buffer, int offset, int count);
}
