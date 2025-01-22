# ThinkingHome.NooLite

[![Build & Tests](https://github.com/thinking-home/noolite/actions/workflows/dotnet.yml/badge.svg)](https://github.com/thinking-home/noolite/actions/workflows/dotnet.yml)
[![NuGet Pre Release](https://img.shields.io/nuget/vpre/ThinkingHome.NooLite.svg)](https://www.nuget.org/packages/ThinkingHome.NooLite)

Библиотека [ThinkingHome.NooLite](https://www.nuget.org/packages/ThinkingHome.NooLite) предоставляет API для управления
устройствами [nooLite](https://www.noo.com.by/sistema-noolite.html) (включая nooLite-F) на платформе .NET Core.
Поддерживается работа с адаптером [MTRF-64-USB](https://www.noo.com.by/mtrf-64-usb.html). Поддерживаются операционные
системы Windows, MacOS, Linux.

## Установка

```
dotnet add package ThinkingHome.NooLite
```

## Пример использования

```csharp
using ThinkingHome.NooLite;

...

static void Main(string[] args)
{
    // параметр конструктора - имя COM порта адаптера
    // при использовании в Windows имя будет похоже на "COM4"
    using (var adapter = new MTRFXXAdapter("/dev/tty.usbserial-AL00HDFI"))
    {
        // добавляем действия при подключени к адаптеру и при отключении
        adapter.Connect += AdapterOnConnect;
        adapter.Disconnect += AdapterOnDisconnect;

        // добавляем обработчики входящих команд
        adapter.ReceiveData += AdapterOnReceiveData;
        adapter.ReceiveMicroclimateData += AdapterOnReceiveMicroclimateData;

        // обработка ошибок
        adapter.Error += AdapterOnError;

        // открываем соединение
        adapter.Open();

        // досрочный выход из сервисного режима
        adapter.ExitServiceMode();

        // включение света в 13 канале (nooLite-F)
        adapter.OnF(13);
    }
}

private static void AdapterOnConnect(object obj)
{
    Console.WriteLine("connect");
}

private static void AdapterOnDisconnect(object obj)
{
    Console.WriteLine("disconnect");
}

private static void AdapterOnReceiveData(object obj, ReceivedData result)
{
    Console.WriteLine(result);
}

private static void AdapterOnReceiveMicroclimateData(object obj, MicroclimateData result)
{
    Console.WriteLine($"temperature: {result.Temperature}, humidity: {result.Humidity}");
}

private static void AdapterOnError(object obj, Exception ex)
{
    Console.WriteLine(ex.Message);
}
```

## API

### Управление нагрузкой

> Перечисленные ниже методы управляют нагрузкой в стандартном режиме nooLite (без шифрования и обратной связи). Для
> каждого метода доступен аналогичный метод с суффиксом `F`, который отправляет ту же команду в режиме nooLite-F.

Включить:

```csharp
void On(byte channel)
```

Выключить:

```csharp
void Off(byte channel)
```

Переключить в противоположное состояние:

```csharp
void Switch(byte channel)
```

Включить на время (`interval` - промежуток времени в пятисекундных интервалах):

```csharp
void TemporarySwitchOn(byte channel, UInt16 interval)
```

Установить уровень яркости:

```csharp
void SetBrightness(byte channel, byte brightness)
```

Запомнить сценарий освещения:

```csharp
void SavePreset(byte channel)
```

Применить сценарий освещения:

```csharp
void LoadPreset(byte channel)
```

Установить цвет светодиодной RGB ленты:

```csharp
void SetLedColor(byte channel, byte valueR, byte valueG, byte valueB)
```

Включить режим плавного изменения цветов светодиодной RGB ленты:

```csharp
void SwitchColorChanging(byte channel)
```

Изменить цвет светодиодной RGB ленты на следующий:

```csharp
void ChangeLedColor(byte channel)
```

Изменить режим светодиодной RGB ленты:

```csharp
void ChangeLedColorMode(byte channel)
```

Изменить скорость смены цветов светодиодной RGB ленты:

```csharp
void ChangeLedColorSpeed(byte channel)
```

### Привязка и отвязка

Привязать силовой блок:

```csharp
void Bind(byte channel)
```

Отвязать силовой блок:

```csharp
void Unbind(byte channel)
```

Перейти в режим привязки для привязки передающего устройства (датчика или пульта):

```csharp
void BindStart(byte channel)
```

Выйти из режима привязки:

```csharp
void BindStop()
```

Очистить привязанные передающие устройства в заданном канале:

```csharp
void ClearChannel(byte channel)
```

Очистить привязанные передающие устройства во всех каналах:

```csharp
void ClearAllChannels()
```

Выйти из сервисного режима:

```csharp
void ExitServiceMode()
```

## Интерфейс командной строки

Кроме пакета `ThinkingHome.NooLite`, предоставляющего API для управления нагрузкой с помощью адаптера nooLite, доступна
утилита `ThinkingHome.NooLite.Console`, которая предоставляет те же самые возможности для управления с помощью
интерфейса командной строки.

### Установка

```shell
$ dotnet tool update --global ThinkingHome.NooLite.Console
```

### Использование

Вывести список портов на компьютере

```shell
$ noolite ports
```

Список доступных команд

```shell
$ noolite --help
```

Описание и список параметров конкретной команды

```shell
# noolite [command] --help

$ noolite set-brightness --help
```

### Пример использования

Включить нагрузку в канале `13` адаптера, подключенного к порту `/dev/tty.usbserial-AL00HDFI` в режиме `noolite-F`

```shell
$ noolite on /dev/tty.usbserial-AL00HDFI 13 -f
```

