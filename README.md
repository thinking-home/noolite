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

Перечень изменений между версиями и указания по переходу на новую мажорную версию —
в [CHANGELOG.md](CHANGELOG.md).

## Пример использования

```csharp
using ThinkingHome.NooLite;

...

static async Task Main(string[] args)
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
        adapter.ReceivePowerUnitState += AdapterOnReceivePowerUnitState;

        // обработка ошибок
        adapter.Error += AdapterOnError;

        // открываем соединение
        adapter.Open();

        // досрочный выход из сервисного режима
        adapter.ExitServiceMode();

        // включение света в 13 канале (nooLite-F)
        adapter.OnF(13);

        // запрос состояния силовых блоков в 13 канале (nooLite-F);
        // каждый блок ответит пакетом Send_State - придёт в ReceivePowerUnitState
        adapter.ReadStateF(13);

        // ответы приходят по радио с задержкой - даём им время дойти
        await Task.Delay(1000);

        // закрываем, дождавшись доставки всего, что уже принято.
        // просто выйти из using нельзя: Dispose закрывает порт немедленно
        // и отбрасывает ещё не обработанные пакеты
        await adapter.FlushAndCloseAsync();
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

private static void AdapterOnReceivePowerUnitState(object obj, PowerUnitStateData result)
{
    Console.WriteLine($"device {result.DeviceId}: {result.State}, power level: {result.PowerLevel}");
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

### Состояние силовых блоков (nooLite-F)

Запросить состояние: по каналу (`deviceId = null` — ответят все привязанные к каналу блоки),
адресно (`deviceId` — 32-битный адрес блока) или широковещательно (`deviceId = 0`). `format` —
адрес строки таблицы состояния, `0` — основная информация.

```csharp
void ReadStateF(byte channel, uint? deviceId = null, byte format = 0)
```

Ответ приходит событиями адаптера:

- `ReceivePowerUnitState` — `PowerUnitStateData` для строки 0: `DeviceType`, `FirmwareVersion`,
  `State` (`Off` / `On` / `TemporaryOn`), `ServiceMode`, `PowerLevel`. Тип устройства и уровень
  мощности отдаются как есть, без интерпретации;
- `ReceiveStateFormatError` — `StateFormatErrorData`, если блок не знает запрошенную строку
  (ответ с форматом 255);
- любая другая строка таблицы — только через общее событие `ReceiveData` сырым пакетом.

Блок nooLite-F присылает своё состояние и без запроса — после каждой команды управления
(`OnF`, `OffF` и т.д.), поэтому `ReceivePowerUnitState` срабатывает чаще, чем вызывается `ReadStateF`.

### Входящие пакеты

Каждый входящий пакет доступен через событие `ReceiveData` как `ReceivedData`. Байт TOGL пакета
имеет разный смысл в зависимости от режима, поэтому доступен тремя свойствами:

- `Remains` (`int?`) — сколько пакетов ответа ещё придёт; только для режимов TX/TXF, иначе `null`;
- `ToggleCounter` (`int?`) — счётчик команд передатчика (датчика, пульта): растёт на единицу при
  каждой новой команде, у повторов одной посылки одинаков; только для режимов RX/RXF, иначе `null`;
- `Togl` (`byte`) — сырой байт, всегда.

#### Как вызываются обработчики

Принятые пакеты ставятся в очередь; события вызываются из **фонового потока** — не из потока,
читающего порт. Поэтому:

- обработчик может работать долго — это не тормозит приём следующих пакетов, отправку команд
  и закрытие адаптера;
- обработчики вызываются **по одному** и в порядке прихода пакетов; для одного пакета сначала
  `ReceiveData`, затем типизированное событие;
- исключение в обработчике попадает в событие `Error`, остальные обработчики и следующие пакеты
  обрабатываются как обычно.

Очередь ограничена — по умолчанию 128 пакетов, задаётся вторым параметром конструктора
`MTRFXXAdapter(portName, queueCapacity)`. При переполнении новый пакет отбрасывается, уже
принятые сохраняются в порядке прихода; число отброшенных — в свойстве `DroppedPacketsCount`.

#### Закрытие

```csharp
void Close()
Task FlushAndCloseAsync(CancellationToken cancellationToken = default)
```

`Close()` (и `Dispose()`) закрывает порт немедленно; пакеты, принятые, но ещё не доставленные
обработчикам, **отбрасываются**. Вызов возвращается сразу после закрытия порта и **не дожидается**
события `Disconnect`: оно приходит вскоре из потока доставки. Кому нужно точно знать момент
отключения — подписывается на событие `Disconnect`. Безопасно вызывать `Close()` изнутри
обработчика события.

`FlushAndCloseAsync()` тоже закрывает порт сразу, но **дожидается**, пока обработчики получат всё,
что уже лежит в очереди; по возврату очередь пуста и `Disconnect` уже вызван. Отмена через токен —
остаток отбрасывается, `Disconnect` всё равно происходит. `FlushAndCloseAsync()` нельзя вызывать
изнутри обработчика события — диспетчер ждал бы сам себя, поэтому бросает `InvalidOperationException`.

В обоих случаях `Disconnect` — **последнее** событие адаптера: обработчики пакета, обработка
которого уже началась в момент закрытия, доводятся до конца (включая типизированные события этого
пакета), и только затем вызывается `Disconnect`; после него обработчики входящих пакетов
не вызываются.

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

### Команды

Команды управления принимают имя порта и номер канала, а режим nooLite-F включается флагом `-f`:

```shell
# noolite [command] [port] [channel] [args...] [-f]
```

| Команда                 | Действие                                                  |
|-------------------------|-----------------------------------------------------------|
| `on`                    | включить нагрузку                                         |
| `off`                   | выключить нагрузку                                        |
| `switch`                | переключить в противоположное состояние                   |
| `temporary-on`          | включить на время (аргумент `interval` в пятисекундных интервалах) |
| `set-brightness`        | установить уровень яркости (аргумент `brightness`, 0..255) |
| `save-preset`           | запомнить сценарий освещения                              |
| `load-preset`           | применить сценарий освещения                              |
| `set-color`             | установить цвет RGB-ленты (аргументы `red`, `green`, `blue`) |
| `change-color`          | сменить цвет RGB-ленты на следующий                       |
| `switch-color-changing` | включить режим плавного изменения цветов                  |
| `change-color-mode`     | сменить режим RGB-ленты                                   |
| `change-color-speed`    | сменить скорость смены цветов                             |
| `bind`                  | привязать силовой блок                                    |
| `unbind`                | отвязать силовой блок                                     |

Команды работы с передающими устройствами (датчиками и пультами) работают только в режиме приёма
и потому **не принимают флаг `-f`**; двум из них не нужен и номер канала:

| Команда         | Аргументы        | Действие                                          |
|-----------------|------------------|---------------------------------------------------|
| `bind-start`    | порт, канал      | перейти в режим привязки передающего устройства   |
| `bind-stop`     | порт             | выйти из режима привязки                          |
| `clear-channel` | порт, канал      | очистить привязки в заданном канале               |
| `clear-all`     | порт             | очистить привязки во всех каналах                 |

Отдельно: `noolite ports` печатает список последовательных портов и не требует адаптера.

Утилита возвращает код `0`, если команда выполнена, и ненулевой код при ошибке — так что её
можно использовать в скриптах.

### Пример использования

Включить нагрузку в канале `13` адаптера, подключенного к порту `/dev/tty.usbserial-AL00HDFI` в режиме `noolite-F`

```shell
$ noolite on /dev/tty.usbserial-AL00HDFI 13 -f
```

