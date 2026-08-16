# Отчёт: read-state

## 1. Точка отсчёта

`dotnet build -c Release` — 0 ошибок, 0 предупреждений. `dotnet test` — 18 пройдено.
Наблюдения с железа из архивного `update-to-net10/report.md`: SUF-1-300 отдаёт `[5, 0, 1, 255]`
при включении и `[5, 0, 0, 0]` при выключении — D0=5, D3=255/0.

## 2. Байт TOGL — три свойства

`ReceivedData`:

| Свойство | Тип | Значение | Для режимов |
|---|---|---|---|
| `Togl` | `byte` | `data[3]` всегда | все |
| `Remains` | `int?` | `data[3]`, иначе `null` | TX, TXF |
| `ToggleCounter` | `int?` | `data[3]`, иначе `null` | RX, RXF |

`ToString()` показывает `remains: N` для TX/TXF, `toggle: N` для RX/RXF, ничего — для Service
и Update. `Remains: int` → `int?` — единственное ломающее изменение; все проекты собрались
без правок, никто не использовал `Remains` как `int`.

Тесты: `Parse_Remains_IsCorrect` заменён на 4 теста (7 кейсов через `[Theory]`): TX/TXF → `Remains`,
RX/RXF → `ToggleCounter`, Service/Update → только `Togl`, повтор RX-пакета → одинаковый счётчик.
18 → 24.

## 3. Два типа для Send_State

- `PowerUnitState { Off = 0, On = 1, TemporaryOn = 2 }`;
- `PowerUnitStateData : ReceivedData` — `DeviceType`, `FirmwareVersion`, `State`, `ServiceMode`,
  `PowerLevel`; константа `MAIN_INFO_FORMAT = 0`;
- `StateFormatErrorData : ReceivedData` — без свойств, константа `ERROR_FORMAT = 255`.

Тесты разбора: 6 на `PowerUnitStateData` (входные байты — с живого SUF-1-300), 1 на
`StateFormatErrorData`. 24 → 31.

## 4. События и обёртка

`MTRFXXAdapter`: `ReceivePowerUnitState` (FMT=0), `ReceiveStateFormatError` (FMT=255); в `TryRead`
после `ReceiveData` ветвление строго по FMT, прочие строки — только `ReceiveData`.

`MTRFXXAdapterExtensions.ReadStateF(channel, deviceId = null, format = 0)` — через `SendData`,
CTR выбирается существующим `GetModeAndAction`: `null` → 0, `0` → 1, иначе → 8.

Тесты на `BuildCommand` для запроса состояния (4 шт.): MODE=2, CTR=0/8/1, CMD=128, FMT в байте 6,
ID в байтах 11–14. **Ограничение**: цепочка `ReadStateF → SendData → GetModeAndAction` юнит-тестом
не покрыта — нет шва для перехвата записи; проверена на железе (раздел 7). 31 → 35.

Публичная поверхность сверена по диффу: одно удаление (`Remains: int`), одно изменение (оно же
→ `int?`), остальное — добавления.

## 5. DebugConsole

Режим `state <port> <channel> [--id N] [--fmt N]`. `-f` подразумевается — `Read_State` есть
только в двусторонней связи. Оба новых события печатаются во всех режимах.

## 6. Сборка и тесты

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35
```

## 7. Проверка на живом адаптере

COM3, SUF-1-300 (ID 33347) на канале 0, PT-111 на канале 1.

### 7.1 Запрос по каналу — `state COM3 0`

```
data: mode: Service, command: None, result: Success, channel: 0, fmt: 0, data: [0, 1, 1, 0], device ID: 4311
data: mode: TXF, command: SendState, result: Success, channel: 0, remains: 0, fmt: 0, data: [5, 0, 0, 0], device ID: 33347
power unit state: ... device type: 5, firmware: 0, state: Off, service mode: False, power level: 0
```

Три наблюдения:
- у сервисного ответа адаптера **нет** `remains:` — для режима Service поле не применимо,
  и `ToString()` это отражает (раньше печатался `remains: 0`);
- `Send_State` пришёл на чистый `Read_State`, без команды управления — цепочка обёртки работает;
- разбор: тип 5, прошивка 0, выключен, мощность 0 — блок в том состоянии, в каком его оставили.

### 7.2 Адресный запрос — `state COM3 0 --id 33347`

Тот же ответ от 33347. CTR=8 распознан блоком.

### 7.3 Неизвестная строка — `state COM3 0 --fmt 200`

```
data: mode: TXF, command: SendState, result: Success, channel: 0, remains: 0, fmt: 255, data: [0, 0, 0, 0], device ID: 33347
state format error: ... fmt: 255 ... state format error: unknown state table row requested
```

**Справка подтверждена на железе:** блок отвечает FMT=255 с нулевыми данными на неизвестную
строку. Сработало `ReceiveStateFormatError`. Первое прямое наблюдение этого поведения на реальном
SUF-1-300.

### 7.4 `on` / `off`

```
power unit state: ... data: [5, 0, 1, 255] ... state: On, service mode: False, power level: 255
power unit state: ... data: [5, 0, 0, 0] ... state: Off, service mode: False, power level: 0
```

Лампа включилась и выключилась. Разобранное состояние приходит после каждой команды —
как и предупреждает doc-комментарий события.

### 7.5 Приём от PT-111 — `ToggleCounter`

`listen COM3`, датчик прогрет дыханием дважды с паузой:

```
data: mode: RX, command: MicroclimateData, result: Success, channel: 1, toggle: 22, fmt: 7, data: [129, 33, 28, 255], device ID: 0
microclimate: ... toggle: 22 ... temperature: 38.5, humidity: 28, low battery: False
data: mode: RX, command: MicroclimateData, result: Success, channel: 1, toggle: 23, fmt: 7, data: [109, 33, 28, 255], device ID: 0
microclimate: ... toggle: 23 ... temperature: 36.5, humidity: 28, low battery: False
```

**Счётчик команд передатчика виден и растёт**: 22 → 23 между двумя посылками. До этого change'а
здесь печаталось `remains: 0`, и драйвер не имел никакой возможности отличить новую посылку
от повтора. Теперь имеет.

Наблюдение: каждая посылка дошла **одним** пакетом. При привязке в прошлом change'е (`bind-rx`)
приходили два подряд (FMT=1 и FMT=0) — там это были разные форматы одной команды, а не повторы.
Повторов одной посылки с одинаковым `toggle` в этом прогоне не поймано: либо адаптер схлопывает
их сам, либо PT-111 в режиме «Датчик» не повторяет. Свойство «новая команда → +1» подтверждено;
свойство «повтор → тот же счётчик» — только юнит-тестом на разбор байтов, на железе не наблюдалось.

### 7.6 Сверка D0/D3

| Байт | update-to-net10 (baseline) | read-state | |
|---|---|---|---|
| D0 (тип) | 5 | 5 | совпадает |
| D3 при On | 255 | 255 | совпадает |
| D3 при Off | 0 | 0 | совпадает |
| D1 (прошивка) | 0 | 0 | совпадает |

Разбор `PowerUnitStateData` возвращает эти значения как есть — без интерпретации, как и решено.

### Итог группы 7

| Проверка | Результат |
|---|---|
| Запрос по каналу | `Send_State` FMT=0 → `PowerUnitStateData` |
| Адресный запрос (CTR=8) | то же от блока 33347 |
| Неизвестная строка (`--fmt 200`) | `Send_State` FMT=255 → `StateFormatErrorData` — **справка подтверждена на железе** |
| `on` / `off` | `state: On` / `Off`, лампа переключилась |
| `ToggleCounter` у PT-111 | 22 → 23 между посылками |
| Сервисный ответ адаптера | без `remains:` — поле не применимо к режиму Service |

## 8. Документация и итог

`README.md`: в пример добавлены подписка на `ReceivePowerUnitState` и вызов `ReadStateF`;
в раздел API — «Состояние силовых блоков (nooLite-F)» с `ReadStateF`, двумя событиями и правилом
для прочих строк; новый подраздел «Входящие пакеты» с тремя свойствами над TOGL.

`docs/device-model-handoff.md` не правился (исторический документ). Пункт «3. Read_State /
Send_State и async-API» закрыт в части `Read_State`/`Send_State`; **async-приём — следующий change**.

### Приёмка

| Критерий | Результат |
|---|---|
| Сборка | 0 ошибок, 0 предупреждений |
| Тесты | **35** (было 18): +7 TOGL, +7 разбор состояния, +4 сборка пакета `Read_State` |
| Публичное API | одно ломающее изменение (`Remains: int` → `int?`), остальное — добавления |
| Живой адаптер | запрос по каналу, адресно, неизвестная строка → FMT=255, `on`/`off`, `ToggleCounter` |

### Предупреждение о состоянии репозитория

Код содержит **ломающее изменение** (`ReceivedData.Remains: int` → `int?`) при `VersionPrefix = 4.4.0`.
Коммитить и вливать можно; **публиковать в NuGet под 4.x нельзя** — потребители, использовавшие
`Remains` как `int`, перестанут компилироваться. Мажорный релиз владелец делает сам, отдельно.

### Что осталось непокрытым

- Цепочка `ReadStateF → SendData → GetModeAndAction` — без юнит-теста (нет шва); проверена
  на железе для CTR=0 и CTR=8, широковещательный CTR=1 (`deviceId = 0`) — только тестом
  на `BuildCommand`.
- Ветвление по FMT в `TryRead` — без юнит-теста; на железе проверены FMT=0 и FMT=255,
  ветка «прочий FMT → только `ReceiveData`» не наблюдалась (нет блока, отдающего другие строки).
- Свойство «повтор посылки → одинаковый `ToggleCounter`» — только тестом на разбор байтов;
  на железе повторов не поймано.

### Для следующего change'а (async-приём)

- События по-прежнему вызываются из-под `lockObject` в потоке таймера — обработчик, который
  делает что-то долгое, тормозит приём и отправку. Это и есть мотивация async-приёма.
- Теперь событий пять: `ReceiveData`, `ReceiveMicroclimateData`, `ReceivePowerUnitState`,
  `ReceiveStateFormatError`, плюс `Connect`/`Disconnect`/`Error`. При проектировании потока
  пакетов стоит решить, идут ли в него типизированные объекты или только `ReceivedData`.
- Схема шва над `SerialPort` и юнит-теста на конкурентную запись — в архивном
  `2026-08-16-thread-safe-send/report.md`; если понадобится тестируемость адаптера, она применима.
