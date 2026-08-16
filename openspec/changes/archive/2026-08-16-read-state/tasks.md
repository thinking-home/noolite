> Шаги, помеченные **[совместно]**, требуют подключённого адаптера и участия владельца.
> Железо: COM3, реле SUF-1-300 (nooLite-F, ID 33347) на канале 0, датчик PT-111 на канале 1.

## 1. Точка отсчёта

- [x] 1.1 Дерево чистое, `dotnet build -c Release` и `dotnet test` зелёные (ожидается 18 тестов,
      0 предупреждений)
- [x] 1.2 Перечитать наблюдения с железа в `openspec/changes/archive/2026-08-16-update-to-net10/report.md`
      (разделы 2.6, 8.2) — реальные значения D0/D3 для SUF-1-300

## 2. Байт TOGL — три свойства

- [x] 2.1 `ReceivedData`: добавить `Togl: byte` (всегда `data[3]`), с doc-комментарием
- [x] 2.2 `ReceivedData`: `Remains` → `int?`, `null` для всех режимов, кроме TX и TXF;
      doc-комментарий — «остаток пакетов ответа, только TX/TXF»
- [x] 2.3 `ReceivedData`: добавить `ToggleCounter: int?`, значение только для RX и RXF;
      doc-комментарий — «счётчик команд передатчика, только RX/RXF»
- [x] 2.4 `ReceivedData.ToString()`: показывать применимое свойство (`remains: N` для TX/TXF,
      `toggle: N` для RX/RXF, ничего для остальных) вместо `remains: 0`
- [x] 2.5 Проверить компиляцию всех проектов после `int` → `int?`: `DebugConsole` и тесты
      могли использовать `Remains` как `int` — собралось без правок, никто не использовал
- [x] 2.6 Тесты `ReceivedData/ParseTests.cs`: обновить `Parse_Remains_IsCorrect` под `int?`;
      добавить проверки TOGL для TX, TXF, RX, RXF, Service — три свойства в каждом
      (заменён на 4 `[Theory]`/`[Fact]`, 7 кейсов; 18 → 24 теста)

## 3. Разбор состояния блока — два типа

- [x] 3.1 Enum `PowerUnitState { Off = 0, On = 1, TemporaryOn = 2 }` — в `ThinkingHome.NooLite/`
      рядом с `ResultCode` (публичный, потребителю нужен)
- [x] 3.2 Класс `PowerUnitStateData : ReceivedData` по образцу `MicroclimateData`: `DeviceType`,
      `FirmwareVersion`, `State`, `ServiceMode`, `PowerLevel`; `ToString()` дополняет базовый.
      Никаких признаков «данные не применимы» — объект создаётся только для FMT=0
- [x] 3.3 Класс `StateFormatErrorData : ReceivedData` — без дополнительных свойств; сам объект
      и есть признак ошибки. `ToString()` дополняет базовый пометкой об ошибке формата
- [x] 3.4 Тесты `PowerUnitStateData/ParseTests.cs`: включён `[5,0,1,255]`, выключен `[5,0,0,0]`,
      включён на время (биты `10`), сервисный бит (0x80), значение `11` в битах состояния
      не роняет разбор — 6 тестов, входные байты взяты с живого SUF-1-300
- [x] 3.5 Тест `StateFormatErrorData`: создаётся из пакета с FMT=255, базовые поля
      (канал, ID устройства, `DataFormat`) доступны

## 4. События и обёртка

- [x] 4.1 `MTRFXXAdapter`: два события — `ReceivePowerUnitState` (`Action<object, PowerUnitStateData>`)
      и `ReceiveStateFormatError` (`Action<object, StateFormatErrorData>`). В `TryRead` после
      `ReceiveData`: `Command == SendState` **и** `DataFormat == 0` → первое; `DataFormat == 255` →
      второе; иной FMT — ничего (design → Decision 3)
- [x] 4.2 `MTRFXXAdapterExtensions`: `ReadStateF(channel, deviceId = null, format = 0)` через
      существующий `SendData`; только F-вариант (design → Decision 4). Doc-комментарий:
      по каналу / адресно / `deviceId == 0` — широковещательно
- [x] 4.3 Тесты на `BuildCommand` для `ReadStateF`: по каналу — MODE=2, CTR=0, CMD=128, FMT=0;
      адресно — CTR=8, ID в байтах 11–14; FMT ≠ 0 — уходит в байт 6. **Ограничение**: тесты
      покрывают сборку пакета, но не цепочку `ReadStateF → SendData → GetModeAndAction` — шва
      нет; цепочка проверяется на железе (7.1–7.3)
- [x] 4.4 Проверить, что публичная поверхность изменилась только добавлениями плюс
      `Remains: int?` — ничего не удалено и не переименовано (сверено по диффу)

## 5. DebugConsole

- [x] 5.1 Режим `state <port> <channel> [--id N] [--fmt N]`: послать `ReadStateF`, подождать
      ответы, печатать `PowerUnitStateData` / `StateFormatErrorData` через оба события.
      `-f` подразумевается — `Read_State` есть только в двусторонней связи
- [x] 5.2 Во всех режимах подписаться на оба события и печатать разобранное состояние —
      оно приходит и после `on`/`off`, полезно видеть

## 6. Сборка и тесты

- [x] 6.1 `dotnet build -c Release` — 0 ошибок, 0 новых предупреждений
- [x] 6.2 `dotnet test` — все зелёные, число тестов выросло относительно 18 — **35**

## 7. Проверка на живом адаптере

- [x] 7.1 **[совместно]** `state COM3 0` — от SUF-1-300 приходит `Send_State`, разобранный
      `PowerUnitStateData` печатается: тип 5, версия 0, состояние Off, мощность 0
- [x] 7.2 **[совместно]** `state COM3 0 --id 33347` — адресный запрос, тот же ответ
- [x] 7.3 **[совместно]** `state COM3 0 --fmt 200` — блок ответил **FMT=255**, сработало
      `ReceiveStateFormatError`. Справка подтверждена на живом железе
- [x] 7.4 **[совместно]** `on` / `off` — в выводе появляется разобранное состояние
      (`State = On` / `Off`), лампа переключается
- [x] 7.5 **[совместно]** `listen` до пакета PT-111 — в выводе `toggle: 22`, затем `toggle: 23`:
      счётчик растёт на единицу между посылками. Повтора с одинаковым счётчиком в прогоне
      не поймано — каждая посылка дошла одним пакетом
- [x] 7.6 Сверить D0/D3 из 7.1 с наблюдениями из `update-to-net10` — совпадает: 5 и 255/0

## 8. Документация и завершение

- [x] 8.1 `README.md` → раздел API: `ReadStateF`, события `ReceivePowerUnitState` /
      `ReceiveStateFormatError`, классы `PowerUnitStateData` / `StateFormatErrorData`;
      три свойства TOGL — новый подраздел «Входящие пакеты»; пример дополнен
- [x] 8.2 `docs/device-model-handoff.md` не править (исторический документ); в отчёте отметить,
      что пункт «3. Read_State / Send_State» закрыт, async-приём — следующий change
- [x] 8.3 Отчёт: результаты 6.1–6.2, 7.1–7.6, вывод с железа дословно; явно указать, что
      `VersionPrefix` не менялся и код содержит ломающее изменение до релиза
