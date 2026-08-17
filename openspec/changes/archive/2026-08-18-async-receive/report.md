# Отчёт: async-receive

## 1. Точка отсчёта

`dotnet build -c Release` — 0 ошибок, 0 предупреждений. `dotnet test` — 35 пройдено.

Поведение до change'а, зафиксированное по коду: `TimerCallback` вызывает
`ThreadSafeExec(true, TryRead, Close)`. Все события — `ReceiveData` и типизированные —
вызываются из `TryRead` **под `lockObject`** в потоке таймера. Исключение в любом обработчике
попадает в `catch` `ThreadSafeExec`, который вызывает `errorHandler` = **`Close`** — то есть
одно исключение в обработчике потребителя закрывало адаптер.

## 2–3. Очередь, диспетчер, закрытие

Реализовано по design; отклонение одно, в задаче 3.4.

**Структура:**

- `Channel<byte[]>`, `CreateBounded(queueCapacity)`, `DropOldest`, `SingleReader`,
  `itemDropped` → `Interlocked.Increment(ref droppedPackets)`;
- `TryRead` под замком: `ReadByte` → маркер → `new byte[17]` → `Read(16)` → `TryWrite`. Ничего больше;
- `DispatchAsync` — `Task.Run` в конструкторе; `WaitToReadAsync` → `TryRead` до дна →
  `Dispatch(bytes)`: `Parse` под `try`, `insideHandler = true`, `Raise` для каждого события
  в прежнем порядке, `insideHandler = false` в `finally`;
- `Raise<T>` ловит исключение обработчика → `RaiseError`; `RaiseError` ловит исключение
  обработчика `Error` → глотает;
- `closing` — volatile bool; диспетчер проверяет перед `Dispatch`; `Open` сбрасывает;
- `Close()` — под замком `closing = true`, таймер стоп, порт закрыт, `DiscardQueue`, `Disconnect`;
- `FlushAndCloseAsync` — `insideHandler` → `InvalidOperationException`; под замком таймер стоп,
  порт закрыт, `drained = new TCS`; ждать, если `Count > 0 || inFlight != 0`; в `finally` —
  `closing = true`, `Disconnect`; при отмене — `DiscardQueue`, пробросить;
- `Dispose()` — `Close()` + `Writer.TryComplete()` + `timer.Dispose()`.

### Отклонение от плана — флаг `inFlight` (задача 3.4)

План: диспетчер после обработчиков пакета проверяет `Count == 0` и сигналит `drained`.
При написании `FlushAndCloseAsync` нашлась гонка: диспетчер делает `TryRead` (`Count → 0`),
**потом** начинает обработчики. `FlushAndCloseAsync` в этом окне видит `Count == 0`, решает
«ждать нечего», зовёт `Disconnect` — а обработчик ещё работает. Нарушение инварианта
«после `Disconnect` тишина».

Решение: `inFlight` (int, `Volatile`) поднимается **до** `TryRead` — сразу после пробуждения
на `WaitToReadAsync` — и снимается после обработчиков всей вычитанной пачки. Диспетчер сигналит
`drained` после этого. `FlushAndCloseAsync` ждёт, если `Count > 0 || inFlight != 0`: хотя бы
одно истинно → диспетчер гарантированно дойдёт до сигнала.

Почему порядок «`inFlight` до `TryRead`» важен: если бы `inFlight` ставился после `TryRead`,
между ними было бы окно с `Count == 0 && inFlight == 0` — та же гонка.

## 4. Сборка, тесты, поверхность

```
Build succeeded.  0 Warning(s)  0 Error(s)
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35
```

Публичная поверхность — только добавления: `DEFAULT_QUEUE_CAPACITY`, optional-параметр
конструктора `queueCapacity`, `DroppedPacketsCount`, `FlushAndCloseAsync`. Console и DebugConsole
компилируются со старым вызовом `new MTRFXXAdapter(port)`.

## 5. DebugConsole

Для проверки были добавлены режимы `slow` (`--sleep`, `--throw`, `--duration`), `flush`
(`--no-flush`), `drop`, `reopen` — ~280 строк. **После проверки удалены по решению владельца**:
одноразовые по духу, а проверка диспетчера переезжает в юнит-тесты следующего change'а
(точка расширения + тесты). Осталось только: печать `DroppedPacketsCount` после `done`, если > 0.

Прогоны с этими режимами — раздел 6 ниже — остаются единственным свидетельством работы
диспетчера на живом железе до появления юнит-тестов.

## 6. Проверка на живом адаптере

COM3, SUF-1-300 (ID 33347) на канале 0, PT-111 на канале 1.

### 6.1 Медленный обработчик — `slow COM3 0 --sleep 3000 --duration 12000`

```
23:47:44.137 handler #1 start: None from 4311
23:47:47.155 handler #1 end
23:47:47.155 handler #2 start: SendState from 33347
23:47:50.156 handler #2 end
23:47:50.157   typed: state=On
23:47:50.157 handler #3 start: SendState from 33347
23:47:53.165 handler #3 end
23:47:53.165   typed: state=Off
...
=== slow result ===
commands sent:      59
handlers started:   5
errors raised:      0
send interval ms:   min 200, max 215, avg 205
dropped packets:    0
port still open:    True
```

**Отправка не ждёт обработчика.** 59 команд за 12 с с интервалом 200–215 мс — ни одного
проседания, хотя обработчик спал по 3 с. `Send_State` от `On` и `Off` вставали в очередь
и доходили по порядку (`state=On`, `state=Off`, `state=On`…). 54 пакета остались в очереди
и были отброшены `Dispose` — ожидаемо для `using`.

### 6.6 Исключение в обработчике — `slow COM3 0 --sleep 500 --throw --duration 5000`

```
23:48:08.716 handler #2 THROWS
23:48:08.717 error: test exception from handler #2
23:48:08.717   typed: state=On          ← типизированное событие того же пакета доставлено
23:48:08.762 handler #3 start           ← следующий пакет обработан
...
=== slow result ===
handlers started:   21
errors raised:      10
port still open:    True                ← адаптер НЕ закрылся
```

**Изоляция подтверждена.** 10 исключений → 10 `Error`, все типизированные события того же
пакета доставлены, следующие пакеты обработаны, порт открыт. До change'а первое же исключение
закрыло бы адаптер.

### 6.2 `FlushAndCloseAsync` — `flush COM3 0`

```
23:48:25.717 handled so far: 2
23:48:25.717 FlushAndCloseAsync()
23:48:25.856 handler #3: SendState
23:48:26.366 handler #4: On
23:48:26.878 disconnect
23:48:26.878 returned after 1161 ms
=== flush result ===
handlers total:           4
handlers after disconnect:0
```

Вернулся через 1161 мс — после того, как диспетчер дообработал два оставшихся пакета
(по 500 мс), затем `disconnect`, затем `return`. **Ноль обработчиков после `Disconnect`.**

Побочно: из 5 команд, посланных за 250 мс, блок ответил `Send_State` только на две; остальное —
эхо `On` с `NoResponse` от адаптера (сериализация «одна команда в полёте», см. `thread-safe-send`).
К диспетчеру не относится.

### 6.3 `Close()` с непустой очередью — `flush COM3 0 --no-flush`

```
23:48:41.533 handled so far: 2
23:48:41.533 Close()
23:48:41.641 disconnect
23:48:41.642 returned after 108 ms
=== flush result ===
handlers total:           2
handlers after disconnect:0
```

Вернулся через 108 мс (закрытие FTDI-порта), остаток отброшен: 2 обработчика против 4 при
`FlushAndCloseAsync`. **Ноль после `Disconnect`** — флаг `closing` отработал.

### 6.4 Переполнение — `drop COM3 0`

```
queue capacity 2, handler sleeps 2 s, sending 10 commands
dropped so far: 8, handled: 2
=== drop result ===
handlers total:  3
dropped packets: 8
(dispatcher must not hang: FlushAndCloseAsync returned)
```

`DropOldest` работает, `DroppedPacketsCount = 8`, диспетчер не завис.

### 3.6 Повторное открытие — `reopen COM3 0`

```
--- round 1: open --- connect, handler #1, handler #2, --- close --- disconnect
--- round 2: open --- connect, handler #3, handler #4, --- close --- disconnect
handlers total: 4
```

Один диспетчер, второй `Open` работает: `closing` сброшен, канал не завершён.

### 6.5 Регрессия — `on` / `off` / `state --fmt 200`

`state: On, power level: 255` → `state: Off, power level: 0`, лампа переключилась;
`--fmt 200` → `StateFormatErrorData`. Всё как в `read-state`, `disconnect` последний.

`listen` до PT-111:

```
data: mode: RX, command: MicroclimateData, result: Success, channel: 1, toggle: 58, fmt: 7, data: [40, 33, 41, 255], device ID: 0
microclimate: ... toggle: 58 ... temperature: 29.6, humidity: 41, low battery: False
```

Порядок `ReceiveData` → `ReceiveMicroclimateData` через диспетчер сохранён; разбор сходится
(`((33 & 0x0F) << 8) + 40 = 296` → 29,6 °C).

### Итог группы 6

| Проверка | Результат |
|---|---|
| Отправка при спящем обработчике | 59 команд, интервал 200–215 мс, ни одного проседания на 3 с |
| Изоляция обработчиков | 10 исключений → 10 `Error`, типизированные доставлены, порт открыт |
| `FlushAndCloseAsync` | вернулся после остатка (1161 мс), 0 обработчиков после `Disconnect` |
| `Close()` с непустой очередью | вернулся за 108 мс, остаток отброшен, 0 после `Disconnect` |
| Переполнение | ёмкость 2 → 8 отброшено, диспетчер не завис |
| Повторный `Open` | оба раунда работают, один диспетчер |
| Регрессия `on`/`off`/`state`/PT-111 | как в `read-state` |

## 7. Документация и итог

`README.md` → «Входящие пакеты»: подразделы «Как вызываются обработчики» (фоновый поток,
по одному, порядок, изоляция, очередь и `DroppedPacketsCount`) и «Закрытие»
(`Close` vs `FlushAndCloseAsync`, `Disconnect` последний, запрет из обработчика).
Doc-комментарии на конструкторе, `Close()`, `FlushAndCloseAsync`, `DroppedPacketsCount`.

### Приёмка

| Критерий | Результат |
|---|---|
| Сборка | 0 ошибок, 0 предупреждений |
| Тесты | 35 (без изменений — диспетчер юнит-тестами не покрыт по решению владельца) |
| Публичное API | только добавления; старый конструктор компилируется |
| Живой адаптер | все семь проверок группы 6 |

### Что не проверено — записано, не спрятано

- **Отмена `FlushAndCloseAsync` посреди обработчика** — нет способа вызвать без шва
  (нужен обработчик, который спит дольше таймаута токена; можно было бы добавить режим
  в DebugConsole, но проверка сводится к «`OperationCanceledException` проброшен, очередь
  очищена, `Disconnect` вызван» — всё это ревью кода в `catch`/`finally`).
- **Битый пакет в `Parse`** — нужны испорченные байты из порта; на живом адаптере не
  воспроизвести. Ветка `catch` → `RaiseError` → `return` — ревью.
- **`FlushAndCloseAsync` из обработчика** — `AsyncLocal`-проверка. Не гонял на железе:
  проверка синхронная и очевидная (`if (insideHandler.Value) throw`), а deadlock без неё
  доказывать на живом адаптере — сомнительное удовольствие.
- **Гонка `inFlight`** — рассуждение в разделе 2–3; на железе окно микросекундное,
  ловить нечем. Ревью.

### Наблюдения для будущего

- `System.Threading.Timer` по-прежнему запускает `TimerCallback` каждые 50 мс независимо
  от предыдущего; с быстрым `TryRead` это уже не копит потоки, но сам подход «таймер + опрос
  `BytesToRead`» остаётся. Событийное чтение (`SerialPort.DataReceived` или `BaseStream.ReadAsync`)
  — отдельная тема, если понадобится снизить латентность.
- Ёмкость 128 при потоке «датчик раз в минуту» — практически бесконечность. Дроп на живом
  железе возможен только с искусственно малой ёмкостью (режим `drop`).
- Пункт «3. Read_State / Send_State и async-API» из `docs/device-model-handoff.md` теперь
  закрыт полностью. Из handoff'а не осталось ничего.
