using System.IO.Ports;

namespace ThinkingHome.NooLite.Internal;

/// <summary>
/// Встроенная реализация порта — тонкая обёртка над <see cref="SerialPort"/> без собственной логики.
/// </summary>
internal sealed class SerialPortDevice : ISerialDevice
{
    private const int BAUD_RATE = 9600;

    // запись выполняется под тем же lockObject адаптера, что и чтение по таймеру,
    // поэтому бесконечное ожидание на записи остановило бы приём пакетов
    private const int WRITE_TIMEOUT = 500;

    private readonly SerialPort port;

    public SerialPortDevice(string portName)
    {
        port = new SerialPort(portName, BAUD_RATE) { WriteTimeout = WRITE_TIMEOUT };
    }

    public bool IsOpen => port.IsOpen;

    public int BytesToRead => port.BytesToRead;

    public void Open() => port.Open();

    public void Close() => port.Close();

    public int ReadByte() => port.ReadByte();

    public int Read(byte[] buffer, int offset, int count) => port.Read(buffer, offset, count);

    public void Write(byte[] buffer, int offset, int count) => port.Write(buffer, offset, count);
}
