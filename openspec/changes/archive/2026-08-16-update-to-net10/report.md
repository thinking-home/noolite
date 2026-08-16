# Отчёт по приёмке: update-to-net10

## 1.1 Исходное состояние (2026-08-16)

**SDK на машине**

```
10.0.400 [C:\Program Files\dotnet\sdk]
```

**Рантаймы**

```
Microsoft.AspNetCore.App 10.0.11
Microsoft.NETCore.App 9.0.19
Microsoft.NETCore.App 10.0.11
Microsoft.WindowsDesktop.App 10.0.11
```

`Microsoft.NETCore.App 9.0.19` установлен владельцем по задаче 1.4 — baseline на net9.0
снимать есть на чём.

**`global.json` до правки**

```json
{ "sdk": { "version": "9.0.100" } }
```

Резолвился в ошибку: «Requested SDK version: 9.0.100 … A compatible .NET SDK was not found».

**TargetFramework** — `net9.0` во всех четырёх проектах.

**Пакеты**

| Проект | Пакет | Версия |
|---|---|---|
| ThinkingHome.NooLite | System.IO.Ports | 9.0.1 |
| ThinkingHome.NooLite.Console | McMaster.Extensions.CommandLineUtils | 4.1.1 |
| ThinkingHome.NooLite.Tests | FakeItEasy | 8.3.0 |
| ThinkingHome.NooLite.Tests | Microsoft.NET.Test.Sdk | 17.12.0 |
| ThinkingHome.NooLite.Tests | xunit | 2.9.3 |
| ThinkingHome.NooLite.Tests | xunit.runner.visualstudio | 3.0.1 |

**CI** — `actions/checkout@v3`, `actions/setup-dotnet@v2`, `dotnet-version: 9.0.x`, `-f net9.0`.

**Package.xml** — `VersionPrefix` 4.4.0, `Copyright` «Thinking-Home.RU © 2025»,
`PackageRequireLicenseAcceptance` продублирован (строки 13 и 14).

## 1.2–1.4 Окружение приведено в рабочее состояние

`global.json` → `10.0.100` + `rollForward: latestFeature`; `dotnet --version` в корне
репозитория возвращает `10.0.400`. .NET 9 Runtime установлен владельцем.

## 1.5 Точка отсчёта: сборка и тесты на net9.0 под SDK 10

`dotnet build ThinkingHome.NooLite.sln -c Release` — **Build succeeded**, 0 ошибок,
**1 предупреждение**:

```
DebugConsole\Program.cs(74,9): warning CS0162: Unreachable code detected
```

`dotnet test ./ThinkingHome.NooLite.Tests -c Release` —
**Passed! Failed: 0, Passed: 18, Skipped: 0, Total: 18**.

Опорные значения для сравнения после переезда: **18 тестов**, **1 предупреждение** (CS0162
в DebugConsole — уйдёт вместе с переписыванием файла на режимы).

## 2.1–2.4 Режимы DebugConsole (на net9.0)

`DebugConsole/Program.cs` переписан на режимы. Собирается с **0 предупреждений** — CS0162 ушёл
вместе с недостижимым кодом.

Покрытие сценариев старого файла:

| Было в старом файле | Стало |
|---|---|
| печать списка портов (стр. 12) | `ports` |
| подписка на события + печать пакетов | во всех режимах, работающих с адаптером |
| `Open` / `ExitServiceMode` (стр. 38–44) | общий пролог всех режимов с адаптером |
| `OnF(13)` / `OffF(13)` (стр. 52–69) | `on` / `off <port> <channel> -f` |
| `OnF(13, 1594)`, `OffF(13, 33347)` (стр. 101–117) | `--id <device id>` |
| `OnF(0)` — «switch on/off» (стр. 122–128) | `on/off <port> 0 -f`; широковещательный вариант — `--id 0` |
| `Bind(2)` (стр. 47–50) | `bind <port> <channel>` |
| `BindF(13)`, `Bind(Mode.NooLiteF, 13)` (стр. 82–91) | `bind <port> <channel> -f` |
| `Unbind` / `UnbindF` (стр. 76–77, 96–97) | `unbind <port> <channel> [-f]` |
| цикл `ClearChannel(ch)` по 64 каналам (стр. 74–77) | `clear <port> <channel>`; очистка всей памяти — `clear-all` |
| — (не было) | `bind-rx` — `BindStart`/`BindStop`, окно привязки датчика 40 с |
| — (не было) | `switch` — `Switch`/`SwitchF` |

Smoke без железа:

```
> ThinkingHome.NooLite.DebugConsole.exe ports
serial port list:
- COM1
exit code: 0

> ThinkingHome.NooLite.DebugConsole.exe on
port name is required
<список режимов>
exit code: 1
```

В списке портов сейчас только `COM1` — адаптер на момент проверки не подключён.

## 2.5 Baseline e2e, связь (net9.0)

Адаптер: FTDI FT232 `VID_0403&PID_6001`, серийный номер **AL00HDFI** (тот же, что был захардкожен
в старом коде как `/dev/tty.usbserial-AL00HDFI`), драйвер FTDI 2.12.36.20, служба `FTSER2K`.
Порт — **COM3**.

```
> ThinkingHome.NooLite.DebugConsole.exe listen COM3
open COM3
connect
exit service mode
listening for incoming packets, press Ctrl+C to stop
data: mode: Service, command: None, result: Success, channel: 0, remains: 0 fmt: 0, data: [0, 1, 1, 0], device ID: 4311
```

Порт открывается, `Connect` срабатывает, на `ExitServiceMode` приходит ответный пакет
`mode: Service`, `result: Success` с собственным nooLite-F адресом адаптера **4311**.

За 45 секунд наблюдения пакетов от датчиков не поступило — привязок датчиков либо нет, либо
датчики в этот интервал не передавали.

## 2.6 Baseline e2e, управление реле (net9.0)

Оборудование: силовой блок **SUF-1-300** (не `-A`), nooLite-F, ID **33347**. Память блока
очищена владельцем по процедуре из руководства SUF-1-300-A (удержание сервисной кнопки ~5 с),
после чего блок привязан заново к **каналу 0**.

**Привязка** — `bind COM3 0 -f`:

```
data: mode: TXF, command: None, result: BindComplete, channel: 0, remains: 0 fmt: 0, data: [5, 0, 0, 0], device ID: 33347
```

`ResultCode.BindComplete` (CTR=3) — привязка выполнена.

**Включение** — `on COM3 0 -f`:

```
data: mode: TXF, command: SendState, result: Success, channel: 0, remains: 0 fmt: 0, data: [5, 0, 1, 255], device ID: 33347
```

**Выключение** — `off COM3 0 -f`:

```
data: mode: TXF, command: SendState, result: Success, channel: 0, remains: 0 fmt: 0, data: [5, 0, 0, 0], device ID: 33347
```

Нагрузка (лампа) физически включилась и выключилась — подтверждено владельцем.

Разбор `Send_State` (FMT=0) по справке из `docs/device-model-handoff.md`:

| Байт | on | off | Смысл |
|---|---|---|---|
| D0 | 5 | 5 | тип устройства |
| D1 | 0 | 0 | версия прошивки |
| D2 | 1 | 0 | состояние: биты 1–0 → `01` включён / `00` выключен |
| D3 | 255 | 0 | уровень мощности |
| TOGL (`remains`) | 0 | 0 | последний пакет серии |

Два расхождения со справкой, зафиксированы как наблюдения (не как дефекты):

- **D0 = 5**, тогда как в справке для SUF-1-300-A указан тип 9. Независимо подтверждает, что
  блок — SUF-1-300 без суффикса `-A`.
- **D3 = 255** при включении, тогда как в справке для релейного режима указано 100. Возможен
  диммерный режим блока либо другая конвенция для этого типа устройства; руководства
  именно на SUF-1-300 найти не удалось (страница модели на сайте производителя отдаёт 404,
  доступен только сканированный каталог без извлекаемого текста).

Ключевой протокольный факт, на который опирается драйвер, подтверждён на живом железе:
**после команды в режиме TXF блок сам присылает `Send_State` с FMT=0** — результат и свежее
состояние приходят одним пакетом.

## 2.7 Baseline e2e, приём от датчика (net9.0)

Датчик: **PT-111**, привязан заново к **каналу 1** через `bind-rx COM3 1` (окно 40 с,
короткое нажатие сервисной кнопки датчика).

```
> ThinkingHome.NooLite.DebugConsole.exe bind-rx COM3 1
binding window is open for channel 1, press Ctrl+C to stop
data: mode: RX, command: Bind, result: Success, channel: 1, remains: 0 fmt: 1, data: [2, 33, 45, 255], device ID: 0
data: mode: RX, command: Bind, result: Success, channel: 1, remains: 0 fmt: 0, data: [2, 33, 45, 255], device ID: 0
```

**Закрыт открытый вопрос из `docs/device-model-handoff.md`:** пакеты датчиков приходят
в **MODE=1 (RX)** — ожидание из документа подтверждено на живом железе. Поле `device ID` = 0:
у батарейных однонаправленных датчиков 32-битного адреса нет, в отличие от nooLite-F блока.
Пакет пришёл дважды (FMT=1 и FMT=0) — передатчики повторяют посылки, как и описано в протоколе.

### Находка для следующего change'а: TOGL недоступен для RX

В выводе `remains: 0`, хотя по протоколу для RX/RX-F байт TOGL — это счётчик новых команд,
по которому потребитель должен дедуплицировать повторные посылки передатчика.
Библиотека обнуляет его намеренно: `ThinkingHome.NooLite/ReceivedData.cs:37`

```csharp
public int Remains => (Mode == MTRFXXMode.RX) | (Mode == MTRFXXMode.RXF) ? 0 : data[3];
```

То есть для датчиков байт TOGL из публичного API недоступен, а драйверу
`ThinkingHome.DeviceModel` он нужен — в handoff'е это указано прямо: «каждый пакет целиком
с полями Mode / Ctr(Result) / Togl(Remains) / … — драйверу нужны все».

Это **не дефект текущего change'а** (переезд на .NET 10 поведения не меняет) — материал
для change'а про async-приём: там при проектировании потока `ReceivedData` нужно решить,
как отдавать сырой TOGL для RX.

### Приём данных микроклимата

`listen COM3`, датчик прогрет дыханием:

```
data: mode: RX, command: MicroclimateData, result: Success, channel: 1, remains: 0 fmt: 7, data: [134, 33, 26, 255], device ID: 0
microclimate: mode: RX, command: MicroclimateData, result: Success, channel: 1, remains: 0 fmt: 7, data: [134, 33, 26, 255], device ID: 0, temperature: 39, humidity: 26, low battery: False
```

Соответствует справке: `Sens_Temp_Humi` = команда 21 (`MicroclimateData`), **FMT=7**.
Сработали **оба** события — и `ReceiveData`, и `ReceiveMicroclimateData`.

Проверка разбора вручную (`MicroclimateData.cs`), D1=134, D2=33, D3=26:

| Величина | Вычисление | Результат |
|---|---|---|
| Температура | `((33 & 0x0F) << 8) + 134 = 390`, делить на 10 | **39,0 °C** |
| Тип датчика | `(33 >> 4) & 0b111 = 2` → PT111 | влажность читается |
| Влажность | D3 | **26 %** |
| Разряд батареи | `33 >> 7 = 0` | **False** |

39 °C — результат прогрева дыханием, значение правдоподобное. Разбор библиотеки совпадает
с ручным расчётом по всем четырём полям.

## 2.8 Baseline зафиксирован

Опорные значения для сравнения после переезда на .NET 10:

| Проверка | Baseline (net9.0) |
|---|---|
| Сборка решения | Build succeeded, 0 ошибок, 1 предупреждение (CS0162, устранено) |
| Тесты | 18 пройдено, 0 упало |
| Связь с адаптером | ответ `mode: Service`, `result: Success`, ID адаптера **4311** |
| Привязка блока | `result: BindComplete`, ID блока **33347** |
| Включение | `SendState`, data `[5, 0, 1, 255]`, лампа загорелась |
| Выключение | `SendState`, data `[5, 0, 0, 0]`, лампа погасла |
| Привязка датчика | `mode: RX`, `command: Bind`, канал 1 |
| Приём микроклимата | `mode: RX`, `command: MicroclimateData`, FMT=7, 39,0 °C / 26 % / батарея в норме |

Конфигурация железа для повторного прогона: порт **COM3**, реле SUF-1-300 (nooLite-F, ID 33347)
на **канале 0**, датчик PT-111 на **канале 1**.

## 3. Смена TFM на net10.0

Все четыре проекта переведены на `net10.0`. Промежуточная зелёная точка **на старых версиях
пакетов**: `Build succeeded, 0 Warning(s), 0 Error(s)`, тесты — 18 пройдено на
`.NETCoreApp,Version=v10.0`. Правок в коде не потребовалось (задача 3.6 не выполнялась).

CS0162 из baseline исчез — недостижимый код ушёл вместе с переписыванием `DebugConsole`.

## 4. Обновление зависимостей

| Пакет | Было | Стало | Результат |
|---|---|---|---|
| `System.IO.Ports` | 9.0.1 | **10.0.11** | сборка чистая, 18 тестов |
| `McMaster.Extensions.CommandLineUtils` | 4.1.1 | **5.1.0** | мажор прошёл **без правок** `Console/Program.cs` |
| `xunit` | 2.9.3 | 2.9.3 | не обновлялся — решение владельца |
| `xunit.runner.visualstudio` | 3.0.1 | 3.0.1 | не обновлялся |
| `Microsoft.NET.Test.Sdk` | 17.12.0 | 17.12.0 | не обновлялся |
| `FakeItEasy` | 8.3.0 | — | удалён (не использовался) |

### Отклонение от плана: переезд на xunit.v3 выполнен и откачен

`xunit.v3` 4.0.0 был подключён, тест-проект переведён в `OutputType=Exe`. Сборка прошла
**без единой правки в четырёх тестовых файлах**, но `dotnet test` упал:

```
Microsoft.Testing.Platform.MSBuild.targets(320,5): error :
Testing with VSTest target is no longer supported by Microsoft.Testing.Platform
on .NET 10 SDK and later.
```

Причина: `xunit.v3` 4.0.0 несёт MTP v2, а под .NET 10 SDK VSTest-мост в MTP удалён.
Обходной флаг `TestingPlatformDotnetTestSupport` работает только до MTP v1.

Проверен рабочий путь — MTP-режим через секцию `test` в `global.json`:

```
Test run summary: Passed!  total: 18  failed: 0  succeeded: 18  skipped: 0
```

Дополнительно проверено, что под MTP `Microsoft.NET.Test.Sdk` и `xunit.runner.visualstudio`
не нужны — без них те же 18 тестов проходят.

**Решение владельца: тестовые пакеты не обновлять.** Конфигурация откачена на VSTest-стек;
секция `test` из `global.json` убрана. Обоснование — в `design.md` → Decision 3.

## 5. Удаление неиспользуемого

- `FakeItEasy` 8.3.0 — удалён из `.Tests.csproj` (не использовался ни одним тестом).
- `<AllowUnsafeBlocks>` — удалён из `ThinkingHome.NooLite.csproj` (в репозитории нет ни одного
  `unsafe` / `stackalloc` / `fixed`).
- Дублирующаяся строка `<PackageRequireLicenseAcceptance>` в `Package.xml` — убрана.
- `Copyright` — 2025 → 2026. `VersionPrefix` не тронут, остаётся 4.4.0.

## 6. Сборка и тесты после всех правок

```
Build succeeded.  0 Warning(s)  0 Error(s)
Test run for ...\bin\Release\net10.0\ThinkingHome.NooLite.Tests.dll (.NETCoreApp,Version=v10.0)
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18
```

Сверка с baseline: **18 тестов = 18 тестов**, предупреждений стало **0 вместо 1** (CS0162 устранён).
Новых предупреждений нет.

## 7. Проверка dotnet tool `noolite`

```
> dotnet pack ./ThinkingHome.NooLite.Console -c Release -o <папка>
Successfully created package 'ThinkingHome.NooLite.Console.4.4.0.nupkg'.

> dotnet tool install --global --add-source <папка> ThinkingHome.NooLite.Console
Tool 'thinkinghome.noolite.console' (version '4.4.0') was successfully installed.

> noolite --help
nooLite command line interface - v4.4.0
<11 команд: bind, change-color, load-preset, off, on, ports, save-preset,
 set-brightness, set-color, switch, unbind>

> noolite ports
Serial port list:
- COM1
- COM3

> dotnet tool uninstall --global ThinkingHome.NooLite.Console
Tool 'thinkinghome.noolite.console' (version '4.4.0') was successfully uninstalled.
```

Справка со всеми командами выводится — значит `McMaster` 5.x работает; `ports` видит COM3 —
значит `System.IO.Ports` 10.x работает из установленного tool-пакета. Список глобальных
инструментов после удаления пуст.

Замечание, не влияющее на приёмку: `dotnet pack` выводит предупреждение об отсутствии readme
в пакете (`aka.ms/nuget/authoring-best-practices/readme`). Оно было и до change'а.

## 8. E2E на живом адаптере после переезда (net10.0)

Конфигурация та же, что в baseline: COM3, реле SUF-1-300 (ID 33347) на канале 0,
датчик PT-111 на канале 1. Перепривязка не выполнялась.

**8.1 Связь** — `listen COM3`:

```
data: mode: Service, command: None, result: Success, channel: 0, remains: 0 fmt: 0, data: [0, 1, 1, 0], device ID: 4311
```

**8.2 Управление** — `on COM3 0 -f`, затем `off COM3 0 -f`:

```
data: mode: TXF, command: SendState, result: Success, channel: 0, remains: 0 fmt: 0, data: [5, 0, 1, 255], device ID: 33347
data: mode: TXF, command: SendState, result: Success, channel: 0, remains: 0 fmt: 0, data: [5, 0, 0, 0], device ID: 33347
```

Лампа загорелась и погасла — подтверждено владельцем.

**8.3 Приём** — `listen COM3`, датчик прогрет дыханием:

```
data: mode: RX, command: MicroclimateData, result: Success, channel: 1, remains: 0 fmt: 7, data: [20, 33, 43, 255], device ID: 0
microclimate: ... temperature: 27.6, humidity: 43, low battery: False
```

### 8.4 Сверка с baseline

| Проверка | Baseline (net9.0) | После переезда (net10.0) | Совпадение |
|---|---|---|---|
| Связь: MODE / result / data / ID | `Service` / `Success` / `[0,1,1,0]` / 4311 | то же | **точное** |
| Включение: MODE / command / data / ID | `TXF` / `SendState` / `[5,0,1,255]` / 33347 | то же | **точное** |
| Выключение: data | `[5,0,0,0]` | `[5,0,0,0]` | **точное** |
| Физическая реакция лампы | вкл / выкл | вкл / выкл | **точное** |
| Датчик: MODE / command / FMT / канал / ID | `RX` / `MicroclimateData` / 7 / 1 / 0 | то же | **точное** |
| Датчик: значения | 39,0 °C / 26 % / батарея ОК | 27,6 °C / 43 % / батарея ОК | **ожидаемо иные** |

Расхождение только в измеренных значениях микроклимата — это разные моменты замера
(в baseline датчик был сильнее прогрет дыханием). Структура пакета, режим, команда, формат,
канал и ID совпадают точно.

Разбор проверен вручную для нового пакета, D1=20, D2=33, D3=43:
`((33 & 0x0F) << 8) + 20 = 276` → **27,6 °C**; `(33 >> 4) & 0b111 = 2` → PT111 → влажность
**43 %**; `33 >> 7 = 0` → батарея в норме. Совпадает с выводом библиотеки.

Оба события — `ReceiveData` и `ReceiveMicroclimateData` — сработали, как и в baseline.

**Вывод: переезд на .NET 10 не изменил поведение библиотеки на живом железе.**

## 9. CI

`.github/workflows/dotnet.yml`:

| Что | Было | Стало |
|---|---|---|
| `actions/checkout` | v3 | **v7** |
| `actions/setup-dotnet` | v2 | **v6** |
| `dotnet-version` | 9.0.x | **10.0.x** |
| `-f` в шаге Test | net9.0 | **net10.0** |

Изменения внесены владельцем коммитом `936440a` «обновлен CI» в ветке `update-to-net10`,
оформлен **PR #8**, workflow отработал **зелёным**, ветка влита в `master` мерджем `abc5751`.
Откат мажоров actions не потребовался — `checkout@v7` и `setup-dotnet@v6` заработали
с первого раза.

Замечание про триггеры (workflow настроен только на `push`/`pull_request` в `master`)
оказалось учтено: сборка запустилась именно по pull request'у.

### Финальная проверка на слитом master

После мерджа и подтягивания свежего `master` (дерево чистое, синхронизировано с `origin/master`):

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed!  - Failed: 0, Passed: 18, Skipped: 0, Total: 18
```

Содержимое `global.json` и `.github/workflows/dotnet.yml` на `master` соответствует изменениям
change'а — мердж ничего не потерял.

## 10. Завершение

### 10.1 Сверка с `docs/device-model-handoff.md`, раздел «1. Обновление до .NET 10»

| Пункт из handoff | Статус |
|---|---|
| `TargetFramework` → `net10.0` во всех четырёх `.csproj` | **выполнено** |
| `System.IO.Ports` → 10.x | **выполнено** (10.0.11) |
| `global.json` → SDK 10.0.x | **выполнено** (10.0.100 + `rollForward: latestFeature`) |
| Тестовые пакеты — актуальные | **отклонено** решением владельца, см. design → Decision 3. `FakeItEasy` при этом удалён как неиспользуемый |
| CI: `dotnet-version: 10.0.x`, `-f net10.0`, актуальные мажоры actions | **выполнено** в рабочем дереве, не запушено |
| `McMaster.Extensions.CommandLineUtils` — проверить совместимость | **выполнено**: обновлён до 5.1.0, работает без правок кода |
| `Package.xml`: год в `Copyright` | **выполнено** (2026) |
| `Package.xml`: `VersionPrefix` 4.5.0 или 5.0.0 | **отклонено** решением владельца — остаётся 4.4.0, релиз в NuGet вне объёма change'а |

### 10.2 Документация

`README.md` упоминаний .NET 9 / net9.0 не содержит — платформа названа «.NET Core» без версии,
обновлять нечего. Публичное API библиотеки не менялось, поэтому раздел API в README актуален.
`DebugConsole` в README не документирован (внутренний отладочный инструмент, не пакуется),
описывать режимы негде.

`docs/device-model-handoff.md` содержит упоминания net9.0 (строки 27, 34), но это исторический
документ — запись контекста сессии от 2026-08-14. Намеренно не правился: переписывание
исторической записи исказило бы её. Решение о том, добавлять ли туда отметку о выполнении
раздела «1», оставлено владельцу.

### 10.3 Итог приёмки

| Критерий | Результат |
|---|---|
| `dotnet build -c Release` всего решения | **0 ошибок, 0 предупреждений** (было 1 — CS0162, устранён) |
| `dotnet test` | **18 пройдено, 0 упало** — столько же, сколько в baseline |
| `dotnet pack` + `tool install` + `noolite --help` / `ports` + `tool uninstall` | **работает**, машина возвращена в исходное состояние |
| E2E на живом адаптере: связь, управление реле, приём от датчика | **совпал с baseline** по режиму, команде, формату, каналу и ID |
| Зелёный CI-workflow | **пройден** — PR #8, влито в `master` мерджем `abc5751` |

**Принятые отклонения:**

1. Тестовые пакеты не обновлены (`xunit` 2.9.3, `xunit.runner.visualstudio` 3.0.1,
   `Microsoft.NET.Test.Sdk` 17.12.0) — решение владельца после разбора цены переезда на MTP.
   Переезд на `xunit.v3` выносится в отдельный change.
2. `VersionPrefix` остаётся 4.4.0, публикации в NuGet нет — по решению владельца, принятому
   на этапе планирования.
Все задачи change'а закрыты.

**Оставшиеся предупреждения:** `dotnet pack` сообщает об отсутствии readme в NuGet-пакете
(`NU5039`-подобное предупреждение best practices). Существовало до change'а, к переезду
отношения не имеет.

### Материал для следующих change'ей

1. **`ReceivedData.Remains` обнуляется для RX/RXF** (`ThinkingHome.NooLite/ReceivedData.cs:37`) —
   байт TOGL, нужный драйверу для дедупликации повторных посылок датчиков, недоступен через
   публичное API. Подтверждено на живом железе: у пакетов PT-111 `remains: 0`.
2. **Переезд на `xunit.v3` / Microsoft.Testing.Platform** — проверено, что тесты мигрируют
   без единой правки в исходниках; цена в сопутствующих изменениях (`global.json`, CI, Rider).
3. **Тип устройства SUF-1-300 = 5**, а не 9 как у SUF-1-300-A; `D3` при включении = 255,
   а не 100. При реализации разбора `Send_State` не закладываться на константы из справки.
