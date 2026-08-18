## 1. Асинхронный каркас утилиты

- [x] 1.1 Перевести `Main` в `ThinkingHome.NooLite.Console/Program.cs` на `async Task<int>`,
      возвращать код из `app.ExecuteAsync(args)` вместо присваивания `Environment.ExitCode`
- [x] 1.2 Добавить каркас `InvokeAsync(string portName, Action<MTRFXXAdapter> action)`:
      `using` над адаптером → `Open()` → `ExitServiceMode()` → `await Task.Delay(50)` →
      `action` → `await Task.Delay(100)` → `await FlushAndCloseAsync()`
- [x] 1.3 Переписать существующий `Invoke(args, action, actionF)` как обёртку над `InvokeAsync`,
      сохранив выбор режима по флагу `-f`; перевести все 10 существующих команд
      на `OnExecuteAsync`
- [x] 1.4 Обработка ошибок: один `try` вокруг `ExecuteAsync` — ветка `CommandParsingException`
      (сообщение + справка + ненулевой код) и ветка прочих исключений (сообщение в поток ошибок
      без стека + ненулевой код)
- [x] 1.5 Убедиться, что успешное выполнение команды возвращает нулевой код возврата

## 2. Версия утилиты

- [x] 2.1 Добавить в `Package.xml` свойство
      `IncludeSourceRevisionInInformationalVersion = false`
- [x] 2.2 Заменить в `app.Description` сборку строки из `ver.Revision` на значение
      `AssemblyInformationalVersionAttribute` с отсечением всего от символа `+`
- [x] 2.3 Добавить опцию `--version` со значением из `GetVersion(assembly)`

## 3. Новые команды

- [x] 3.1 `temporary-on` — `TemporarySwitchOn`/`TemporarySwitchOnF`, аргументы:
      порт, канал, `interval` (в пятисекундных интервалах), флаг `-f`
- [x] 3.2 `switch-color-changing` — `SwitchColorChanging`/`F`, стандартная форма аргументов
- [x] 3.3 `change-color-mode` — `ChangeLedColorMode`/`F`, стандартная форма аргументов
- [x] 3.4 `change-color-speed` — `ChangeLedColorSpeed`/`F`, стандартная форма аргументов
- [x] 3.5 `bind-start` — `BindStart`, аргументы: порт, канал; **без** флага `-f`
- [x] 3.6 `bind-stop` — `BindStop`, аргумент: только порт; **без** флага `-f` и без канала
- [x] 3.7 `clear-channel` — `ClearChannel`, аргументы: порт, канал; **без** флага `-f`
- [x] 3.8 `clear-all` — `ClearAllChannels`, аргумент: только порт; **без** флага `-f`
      и без канала
- [x] 3.9 Зарегистрировать все новые команды в `Main` и снабдить каждую описанием
      (`cmd.Description`) в стиле существующих

## 4. Релизные артефакты

- [x] 4.1 Создать `CHANGELOG.md` в формате Keep a Changelog, на русском, начиная с версии 5.0.0;
      добавить ссылку на историю релизов в git для версий ниже 5.0
- [x] 4.2 Описать в разделе ломающих изменений 5.0.0: переход на `net10.0`; вызов событий
      из фонового потока по одному и в порядке прихода; изменённая семантика
      `Close()`/`Dispose()` (отбрасывают недоставленное) и новый `FlushAndCloseAsync()`;
      `ReceivedData.Remains` `int` → `int?`
- [x] 4.3 Описать в разделе добавленного 5.0.0: `ToggleCounter` и `Togl`; `ReadStateF`
      с событиями `ReceivePowerUnitState` и `ReceiveStateFormatError`; ограниченная очередь
      приёма с `DroppedPacketsCount` и параметром `queueCapacity` конструктора
- [x] 4.4 Описать в CHANGELOG изменения утилиты из этого change'а: новые команды, коды возврата
      и сообщения об ошибках, завершение без отбрасывания принятого, опция `--version`
- [x] 4.5 Поднять `VersionPrefix` в `Package.xml` с `4.4.0` до `5.0.0`

## 5. Документация

- [x] 5.1 Обновить раздел «Интерфейс командной строки» в `README.md`: привести список команд
      в соответствие с фактическим набором, отметить команды без поддержки режима nooLite-F
- [x] 5.2 Упомянуть в `README.md` наличие `CHANGELOG.md` как источника сведений о миграции

## 6. Проверка

- [x] 6.1 `dotnet build` решения без ошибок и новых предупреждений
- [x] 6.2 `dotnet test ./ThinkingHome.NooLite.Tests` — все 76 существующих тестов зелёные
      (код библиотеки не менялся)
- [x] 6.3 Проверить `--help` без аргументов и по каждой новой команде: состав и порядок
      аргументов, наличие или отсутствие флага `-f`, текст описания
- [x] 6.4 Проверить `noolite --version` и строку версии в общей справке — совпадают с `5.0.0`
- [x] 6.5 Проверить код возврата: успешная команда → `0`; вызов с несуществующим именем порта →
      сообщение без стека и ненулевой код; вызов с недостающим аргументом → справка
      и ненулевой код
- [x] 6.6 Дымовой прогон на живом железе (адаптер COM3, реле SUF-1-300 на канале 0):
      `ports`, `on -f`, `off -f`, `temporary-on -f`, `set-brightness -f`.
      Команды `clear-channel` и `clear-all` на стенде **не выполнять** — они разрушают привязки
