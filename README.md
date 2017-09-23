# ThinkingHome.NooLite [![NuGet Pre Release](https://img.shields.io/nuget/vpre/ThinkingHome.NooLite.svg)](https://www.nuget.org/packages/ThinkingHome.NooLite/4.0.0-beta1)

Библиотека [ThinkingHome.NooLite](https://www.nuget.org/packages/ThinkingHome.NooLite/4.0.0-beta1) предоставляет API для управления устройствами [nooLite](https://www.noo.com.by/sistema-noolite.html) (включая nooLite-F) на платформе .NET Core. Поддерживается работа с адаптером [MTRF-64-USB](https://www.noo.com.by/mtrf-64-usb.html). Поддерживаются операционные системы Windows, MacOS, Linux.

## Установка

Package Manager

```
Install-Package ThinkingHome.NooLite -Version 4.0.0-beta1
```

.NET CLI

```
dotnet add package ThinkingHome.NooLite --version 4.0.0-beta1
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
        adapter.Connected += AdapterOnConnected;
        adapter.Disconnected += AdapterOnDisconnected;

        // добавляем обработчик входящих команд
        adapter.DataReceived += AdapterOnDataReceived;
    
        // открываем соединение
        adapter.Open();
    
        // досрочный выход из сервисного режима
        adapter.ExitServiceMode();
    
        // включение света в 13 канале (nooLite-F)
        adapter.OnF(13);
    }
}

private static void AdapterOnConnected(object o)
{
    Console.WriteLine("connected");
}

private static void AdapterOnDisconnected(object o)
{
    Console.WriteLine("disconnected");
}

private static void AdapterOnDataReceived(object o, ReceivedData result)
{
    //var msg = string.Join("=", bytes.Select(b => b.ToString()));
    Console.WriteLine(result);
}
```

## API

### Управление нагрузкой

> Перечисленные ниже методы управляют нагрузкой в стандартном режиме nooLite (без шифрования и обратной связи). Для каждого метода доступен аналогичный метод с суффиксом `F`, который отправляет ту же команду в режиме nooLite-F.

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
