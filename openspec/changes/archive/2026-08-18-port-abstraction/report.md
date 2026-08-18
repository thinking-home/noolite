# Отчёт: port-abstraction

## 1. Точка отсчёта

Дерево чистое (`master`, c160e56). `dotnet build -c Release` — 0 ошибок, 0 предупреждений.
`dotnet test` — 35 пройдено (только разбор байтов).

Состав интерфейса сверен с кодом: у поля `device` в `MTRFXXAdapter.cs` используются ровно
`IsOpen`, `BytesToRead`, `ReadByte`, `Read`, `Open`, `Close`, `Write` (11 обращений) плюс
`WriteTimeout` при создании `SerialPort`. Список из design → Decision 1 совпадает.

## 2. Шов в библиотеке

- `Internal/ISerialDevice.cs` — `internal interface`, семь членов, сигнатуры как у `SerialPort`.
  Без `WriteTimeout` (переехал в обёртку) и без `Dispose` (адаптер порт не освобождает).
- `Internal/SerialPortDevice.cs` — `internal sealed`, `new SerialPort(portName, 9600)
  { WriteTimeout = 500 }`, все члены делегируют. Комментарий о причине таймаута — здесь.
- `MTRFXXAdapter.cs`: поле `ISerialDevice device`; `internal MTRFXXAdapter(ISerialDevice, int)`
  с `ArgumentNullException.ThrowIfNull` и прежней проверкой ёмкости; публичный строковый
  конструктор делегирует `this(new SerialPortDevice(portName), queueCapacity)`. Удалены
  `WRITE_TIMEOUT` и `using System.IO.Ports`. Остальной код адаптера не тронут.
- `ThinkingHome.NooLite.csproj` — `<InternalsVisibleTo Include="ThinkingHome.NooLite.Tests"/>`.

Дифф библиотеки: `MTRFXXAdapter.cs` +12/−8, `.csproj` +3, два новых файла. В диффе ни одного
нового `public`; оба новых типа `internal`; `MTRFXXAdapterExtensions` не тронут. Console
и DebugConsole компилируются без правок.

## 3. Инфраструктура тестов

- `FakeSerialDevice.cs` — потокобезопасный буфер входящих (`Feed`), журнал исходящих
  (`Written`, `WrittenPackets` по 17), `Write` по байту с `Thread.Yield()`, `Write` при закрытом
  порте → `InvalidOperationException` (как `SerialPort`), `FailRead` (одноразово на
  `BytesToRead`) / `FailWrite`, счётчики `OpenCount` / `CloseCount`.
- `AdapterTestKit.cs` — коллекция `adapter` с `DisableParallelization`; `Packets` (Send_State
  FMT 0/255/16, микроклимат PT-111, `Untyped` с меткой в Data1 и адресе, `Broken`); `Wait`
  (`For` с таймаутом 5 с, `Until` с опросом 10 мс, `Grace` 250 мс для отрицательных проверок);
  `EventLog` (потокобезопасный журнал + `WaitForCount`); `Gate` (медленный обработчик:
  сигнал «начался», ручное отпускание).

Отклонений от design нет. Единственная поправка по ходу: в `CloseTests` журнал подписан
**до** `Gate`, чтобы «пакет дошёл до обработчика» писалось на входе, а не после отпускания.

## 4. Тесты — 38 новых, 73 всего

| Файл | Тестов | Что покрывает |
|---|---|---|
| `LifecycleTests` | 10 | null-порт, ёмкость ≤ 0 (×2), Open/Close, повторный Open, Close закрытого, Dispose ×2, reopen, отказ порта при опросе |
| `SendCommandTests` | 4 | атомарность 8×40, ошибка записи → вызывающему, отправка при закрытом порту, отправка при стоящем обработчике |
| `ExtensionsSendTests` | 5 | `ReadStateF` по каналу / по ID / с FMT, `OnF`, `Off` → байты в порту = `BuildCommand` |
| `ReceiveTests` | 8 | FMT 0/255/16, микроклимат, порядок A-B-C, чтение при стоящем обработчике, битый пакет, мусор до маркера |
| `HandlerIsolationTests` | 2 | бросающий обработчик, бросающий обработчик Error |
| `QueueTests` | 2 | переполнение при ёмкости 2, ёмкость по умолчанию 128 |
| `CloseTests` | 7 | Close с очередью, Flush с очередью, отмена, из обработчика, пустая очередь, закрытый порт, Dispose с очередью |

У каждого теста — комментарий: что проверяет, в каком контексте, к какому сценарию спеки
относится (или «существующее поведение, спекой не описано»).

Привязка к спекам — таблица в design → Decision 6, все строки реализованы. Уточнение
к строке «ёмкость по умолчанию»: в задаче 5.5 арифметика была «Feed 130 → dropped 1»
(считая P1 в числе 130); тест подкладывает P1 отдельно и затем 130 → в очередь 128,
отброшено 2. Требование то же — ёмкость 128.

### 4.7 Проверка от противного

`lock (lockObject)` в `SendCommand` временно снят, запущен
`SendCommand_FromManyThreads_WritesWholePackets`:

```
Assert.Equal() Failure: Values differ
Expected: 172
Actual:   3
Failed!  - Failed: 1, Passed: 0
```

Упал на проверке стопового маркера — вместо 172 байт `3` (метка другого потока): байты
пакетов действительно перемешались. Замок возвращён, тест снова зелёный. Тест измеряет ровно
то, ради чего написан.

## 5. Сборка и стабильность

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed!  - Failed: 0, Passed: 73, Skipped: 0, Total: 73, Duration: 3-4 s
```

Пять прогонов подряд (`--no-build`) — 5 × 73 зелёных, ни одного мигания. Адаптерные тесты
занимают ~3,5 с из 4 (35 тестов разбора — сотни миллисекунд).

## 6. Находки — код не правился, решение за владельцем

### 6.1 `Close()` во время обработчика → типизированное событие после `Disconnect`

Ожидалось по design → Risks; подтверждено временным зондом (после прогона удалён):

```
пакет Send_State FMT 0; обработчик ReceiveData стоит; Close(); отпустить
ожидание по спеке:  ["data", "disconnect"]
фактически:         ["data", "disconnect", "state"]
```

`closing` проверяется один раз перед `Dispatch(bytes)`; обработчики уже начатого пакета
доигрывают — и второй подписчик `ReceiveData` (multicast), и типизированное
`ReceivePowerUnitState`. Спека packet-receiving: «событие отключения MUST быть последним:
после него обработчики входящих пакетов не вызываются».

Варианты:

1. **Ужесточить код**: проверять `closing` перед каждым `Raise` в `Dispatch` (закроет
   типизированные события; multicast-подписчики одного события — нет, `Invoke` неделим).
2. **Уточнить спеку**: «обработчики уже начатого пакета доигрывают; новые пакеты после
   `Disconnect` не начинаются». Это и есть фактическое поведение, и оно же для
   `FlushAndCloseAsync` при отмене.

Тесты `CloseTests` написаны так, чтобы проверять ровно спековый сценарий («в очереди лежат
необработанные пакеты»): P1 без типизированного разбора, журнал на входе в обработчик.
Краевой случай ими не покрыт — намеренно, до решения.

### 6.2 Порядок `Disconnect` / `Error` при отказе порта

При исключении из `BytesToRead` `ThreadSafeExec` сначала зовёт `errorHandler` (= `Close` →
`Disconnect`), затем `RaiseError` → `Error`. То есть `Error` приходит **после** `Disconnect`.
Спеку это не нарушает (`Error` — не обработчик входящего пакета), тест
`ReadFailure_RaisesError_AndClosesAdapter` порядок не фиксирует. Записано как наблюдение.

## 7. Проверка на живом адаптере (8.1)

COM3, SUF-1-300 (ID 33347) на канале 0, PT-111 на канале 1. DebugConsole, сборка Release
со швом (`SerialPortDevice`).

| Команда | Результат |
|---|---|
| `on COM3 0 -f` | эхо `Service/None` от 4311; `Send_State` `[5, 0, 1, 255]` от 33347 → `state: On, power level: 255`; лампа включилась; `disconnect` последний |
| `off COM3 0 -f` | `Send_State` `[5, 0, 0, 0]` → `state: Off, power level: 0`; лампа выключилась |
| `state COM3 0` | `Send_State` `[5, 0, 0, 0]` → `Off` |
| `state COM3 0 --fmt 200` | `Send_State` FMT 255 → `StateFormatErrorData` («unknown state table row requested») |
| `listen COM3` | за первые ~6 мин — только эхо адаптера; в третьем прогоне (10 мин) четыре пакета PT-111: `RX / MicroclimateData / FMT 7 / канал 1`, toggle 8, 9, 10, 11; каждый — `data:` затем `microclimate:` |

Разбор PT-111 сверен вручную: `[30, 33, 43, 255]` → `((33 & 0x0F) << 8) + 30 = 286` → 28,6 °C,
`(33 >> 4) & 0b111 = 2` → PT111 → влажность 43 %, батарея в норме. Совпадает с выводом.

Всё как в `async-receive/report.md` § 6.5: обёртка над `SerialPort` не меняет ни отправку,
ни приём, ни порядок событий.

## 8. Документация

- `openspec/config.yaml` → `context`: «Известное ограничение…» заменено описанием шва,
  тест-кита и краевого случая 6.1; число тестов 73.
- README не менялся (публичного API не прибавилось).
- `ReadStateCommandTests.cs`: комментарий «цепочка не покрыта» заменён ссылкой на
  `ExtensionsSendTests`.

## Что не проверено

- **`SerialPortDevice`** юнит-тестами не покрыт — обёртка без логики, проверена только на
  железе (раздел 7).
- **Отрицательные проверки** («не доставлен», «событий после Disconnect нет») держатся на
  паузе 250 мс после положительного сигнала — по построению слабее положительных;
  осознанно, см. design → Decision 5.
