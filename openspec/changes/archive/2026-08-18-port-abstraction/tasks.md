> Шаги, помеченные **[совместно]**, требуют подключённого адаптера и участия владельца.
> Железо: COM3, реле SUF-1-300 (nooLite-F, ID 33347) на канале 0, датчик PT-111 на канале 1.
> Если тест выявляет расхождение кода со спекой — записать в отчёт как находку, поведение
> адаптера не править без решения владельца (design → Risks, первый пункт).

## 1. Точка отсчёта

- [x] 1.1 Дерево чистое, `dotnet build -c Release` и `dotnet test` зелёные (35 тестов,
      0 предупреждений) — зафиксировать в отчёте
- [x] 1.2 Подтвердить по `MTRFXXAdapter.cs`, что у `device` используются ровно `IsOpen`,
      `BytesToRead`, `Open`, `Close`, `ReadByte`, `Read`, `Write` и `WriteTimeout` при создании —
      состав интерфейса из design → Decision 1 не расходится с кодом

## 2. Внутренний шов в библиотеке

- [x] 2.1 `ThinkingHome.NooLite/Internal/ISerialDevice.cs` — `internal interface ISerialDevice`
      с семью членами (design → Decision 1); краткие комментарии на членах: `Write` в закрытый
      порт бросает, `Read` вызывается только при достаточном `BytesToRead`
- [x] 2.2 `ThinkingHome.NooLite/Internal/SerialPortDevice.cs` — `internal sealed`, конструктор
      `(string portName)` → `new SerialPort(portName, BAUD_RATE) { WriteTimeout = WRITE_TIMEOUT }`;
      константы `9600` / `500` и комментарий о причине таймаута переезжают сюда из адаптера;
      каждый член делегирует `SerialPort`
- [x] 2.3 `MTRFXXAdapter.cs`: поле `private readonly ISerialDevice device`; новый `internal`
      конструктор `(ISerialDevice device, int queueCapacity = DEFAULT_QUEUE_CAPACITY)` с
      `ArgumentNullException.ThrowIfNull(device)` и прежней проверкой ёмкости; публичный
      строковый конструктор делегирует `this(new SerialPortDevice(portName), queueCapacity)`;
      удалить `WRITE_TIMEOUT` и `using System.IO.Ports` из адаптера. Остальной код не трогать
- [x] 2.4 `ThinkingHome.NooLite.csproj`: `<InternalsVisibleTo Include="ThinkingHome.NooLite.Tests"/>`
- [x] 2.5 `dotnet build -c Release` — 0 ошибок, 0 предупреждений; Console и DebugConsole
      компилируются без правок. Сверить по диффу: публичная поверхность не изменилась
      (ни одного нового `public`)

## 3. Подставной порт и инфраструктура тестов

- [x] 3.1 `ThinkingHome.NooLite.Tests/FakeSerialDevice.cs` по design → Decision 4: буфер входящих
      под замком + `Feed(params byte[][])`; журнал исходящих, `Write` по байту с `Thread.Yield()`,
      `WrittenPackets` (срезы по 17); `Open`/`Close`/`IsOpen`; `Write` при закрытом порте →
      `InvalidOperationException`; `FailRead` / `FailWrite` — исключение по требованию
- [x] 3.2 Хелперы в `TestHelpers.cs` (или рядом): сборка входящих пакетов на базе `GetBytes()` —
      `Packet(command, format, data…)`, готовые `SendStateFmt0`, `SendStateFmt255`,
      `SendStateFmt16`, `Microclimate`, `Untyped` (без типизированного разбора); ожидание
      с таймаутом 5 с (`WaitAsync`), «медленный обработчик» на `ManualResetEventSlim`
      с сигналом «начался»
- [x] 3.3 `[CollectionDefinition("adapter", DisableParallelization = true)]` — все тестовые
      классы адаптера в этой коллекции; существующие тесты разбора не трогать

## 4. Тесты: конструктор, жизненный цикл, отправка

- [x] 4.1 `MTRFXXAdapter/LifecycleTests.cs`: `(ISerialDevice)null` → `ArgumentNullException`;
      ёмкость 0 → `ArgumentOutOfRangeException`; `Open` → `Connect` + `IsOpened` + `fake.IsOpen`;
      `Close` → `Disconnect` + порт закрыт; повторный `Open` — один `Connect`; `Close` закрытого —
      без `Disconnect`; `Dispose` дважды — без исключений
- [x] 4.2 `LifecycleTests`: повторное открытие — пакет после первого `Open` доставлен, после
      `Close`/`Open` следующий пакет тоже доставлен
- [x] 4.3 `LifecycleTests`: `FailRead` → `Error` с этим исключением, `IsOpened == false`,
      `fake.IsOpen == false`, `Disconnect` вызван; `SendCommand` после — исключение
      (существующее поведение, спекой не описано — пометить в комментарии теста)
- [x] 4.4 `MTRFXXAdapter/SendCommandTests.cs`: 8 потоков × 40 пакетов, каждый поток метит
      пакеты своим числом в канале, четырёх байтах данных и адресе; проверить длину журнала
      (кратна 17), маркеры 171/172 в каждом срезе, контрольную сумму, совпадение меток
      внутри среза (схема из `thread-safe-send/report.md`, раздел 4)
- [x] 4.5 `SendCommandTests`: `FailWrite` → то же исключение вызывающему, `IsOpened` остаётся
      `true`; отправка при закрытом порту → `InvalidOperationException`; отправка во время
      заблокированного обработчика возвращается, не дожидаясь отпускания
- [x] 4.6 `MTRFXXAdapter/ExtensionsSendTests.cs`: `ReadStateF(0)`, `ReadStateF(0, 33347)`,
      `ReadStateF(0, format: 16)`, `OnF(0)`, `Off(0)` — записанный пакет побайтно равен
      `BuildCommand(...)` с ожидаемыми MODE/CTR/CMD/FMT/ID; обновить комментарий в
      `ReadStateCommandTests.cs` («цепочка не покрыта») — теперь покрыта
- [x] 4.7 Проверка теста 4.4 от противного: временно снять `lock` в `SendCommand` — тест должен
      упасть; вернуть замок. Записать результат в отчёт

## 5. Тесты: приём, изоляция, очередь

- [x] 5.1 `MTRFXXAdapter/ReceiveTests.cs`: `Send_State` FMT 0 → `ReceiveData`, затем
      `ReceivePowerUnitState`, других событий нет; FMT 255 → `ReceiveData`, затем
      `ReceiveStateFormatError`; FMT 16 → только `ReceiveData`; команда 21 FMT 7 →
      `ReceiveData`, затем `ReceiveMicroclimateData`. Порядок фиксировать общим журналом событий
- [x] 5.2 `ReceiveTests`: A, B, C одним `Feed` при медленном обработчике → доставлены A, B, C
      (сверка по `DeviceId` или байту данных); обработчик заблокирован на A, `Feed(B, C, D)` →
      буфер порта опустел до отпускания (`fake.BytesToRead == 0`), после отпускания B, C, D
      доставлены по порядку
- [x] 5.3 `ReceiveTests`: пакет с неверным стоповым маркером → `Error` (`ArgumentException`
      из `Parse`), следующий корректный пакет доставлен, `IsOpened == true`; мусорные байты
      перед стартовым маркером — пакет доставлен
- [x] 5.4 `MTRFXXAdapter/HandlerIsolationTests.cs`: `ReceiveData` бросает для `Send_State` FMT 0 →
      `Error` с тем же исключением, `ReceivePowerUnitState` того же пакета вызван, следующий
      пакет доставлен, порт открыт; обработчик `Error` бросает → подавлено, следующий пакет
      доставлен
- [x] 5.5 `MTRFXXAdapter/QueueTests.cs`: ёмкость 2, обработчик заблокирован на P1 (сигнал
      «начался» дождаться), `Feed(P2..P5)` → после отпускания доставлены P1, P2, P3 и только они,
      `DroppedPacketsCount == 2`; ёмкость по умолчанию — P1 в обработчике, `Feed` 130 пакетов →
      `DroppedPacketsCount == 1`, `DEFAULT_QUEUE_CAPACITY == 128`

## 6. Тесты: закрытие

- [x] 6.1 `MTRFXXAdapter/CloseTests.cs`: обработчик заблокирован на P1 (пакет **без**
      типизированного разбора), P2, P3 в очереди → `Close()` → `Disconnect`, `fake.IsOpen == false`;
      отпустить; ≥ 200 мс — P2, P3 не доставлены, событий после `Disconnect` нет
- [x] 6.2 `CloseTests`: та же расстановка → `FlushAndCloseAsync()` → порт закрыт сразу, задача
      не завершена; отпустить → P1, P2, P3 доставлены по порядку, `Disconnect` после P3, задача
      завершена
- [x] 6.3 `CloseTests`: та же расстановка, токен отменён при заблокированном обработчике →
      `OperationCanceledException`, `Disconnect`, порт закрыт; отпустить → P2, P3 не доставлены
- [x] 6.4 `CloseTests`: обработчик вызывает `FlushAndCloseAsync()` → `InvalidOperationException`
      (поймать в обработчике, передать в тест через `TaskCompletionSource`)
- [x] 6.5 `CloseTests`: пустая очередь и простаивающий диспетчер → `FlushAndCloseAsync`
      возвращается, `Disconnect` вызван; при закрытом порту → возвращается, `Disconnect`
      не вызван; `Dispose` при непустой очереди — как `Close` (остаток не доставлен, `Disconnect`)
- [x] 6.6 Записать в отчёт краевой случай «`Close()` во время обработчика → типизированное
      событие того же пакета после `Disconnect`» с вариантами решения (design → Risks);
      если какой-то тест 4–6 выявил иное расхождение со спекой — тоже в отчёт, код не править

## 7. Сборка, стабильность, поверхность

- [x] 7.1 `dotnet build -c Release` — 0 ошибок, 0 предупреждений; `dotnet test` — все зелёные
      (35 старых + новые), время прогона адаптерных тестов записать
- [x] 7.2 Стабильность: прогнать `dotnet test` 5 раз подряд — ни одного мигания; если мигает —
      заменить паузу на сигнал или увеличить таймаут (не `Retry`), записать причину
- [x] 7.3 Публичное API без изменений — сверить по диффу: новые типы и конструктор `internal`,
      `MTRFXXAdapterExtensions` не тронут

## 8. Проверка на живом адаптере

- [x] 8.1 **[совместно]** Регрессия через DebugConsole на COM3: `on 0` / `off 0` (лампа
      переключается, `Send_State` `[5,0,1,255]` / `[5,0,0,0]`), `state 0` и `state 0 --fmt 200`
      (состояние / `StateFormatErrorData`), `listen` до пакета PT-111 с канала 1 — обёртка
      `SerialPortDevice` ведёт себя как прежний прямой `SerialPort`

## 9. Документация и завершение

- [x] 9.1 `openspec/config.yaml` → `context`: убрать «Известное ограничение: юнит-тестов
      на адаптер нет», записать наличие internal-шва (`ISerialDevice` / `FakeSerialDevice`)
      и число тестов
- [x] 9.2 Отчёт `report.md`: точка отсчёта (1.1), состав интерфейса (1.2), проверка
      от противного (4.7), находки тестов (6.6), сборка/стабильность (7.1–7.2), железо (8.1);
      отдельно — что не проверено и почему
