> Шаги, помеченные **[совместно]**, требуют подключённого адаптера и участия владельца.
> Железо: COM3, реле SUF-1-300 (nooLite-F, ID 33347) на канале 0, датчик PT-111 на канале 1.
> Опора — 73 теста из `port-abstraction`; они страховка при рефакторинге диспетчера.

## 1. Точка отсчёта

- [x] 1.1 Дерево чистое, `dotnet build -c Release` и `dotnet test` зелёные (73 теста,
      0 предупреждений) — зафиксировать в отчёте
- [x] 1.2 Перечитать в `MTRFXXAdapter.cs` текущие `closing` / `inFlight` / `drained` /
      `insideHandler`, `Close` / `FlushAndCloseAsync` / `Dispose` / `DispatchAsync` — зафиксировать
      исходное поведение и краевой случай (типизированное событие после `Disconnect`)

## 2. Запрос закрытия и диспетчер

- [x] 2.1 `CloseRequest` — приватный класс: `volatile bool Drain`, `readonly TaskCompletionSource
      Done` (для drain — с `RunContinuationsAsynchronously`; для немедленного — `null`); поле
      `private volatile CloseRequest pendingClose`; статический сентинел `WAKE = new byte[0]`
- [x] 2.2 `TryRequestClose(bool drain, out CloseRequest req)` — `Interlocked.CompareExchange`
      публикует запрос, только если `pendingClose == null`; первый выигрывает
- [x] 2.3 Удалить поля `closing`, `inFlight`, `drained`. `insideHandler` оставить
- [x] 2.4 `DispatchAsync`: цикл по элементам — `WAKE` пропускать по `ReferenceEquals`; иначе
      прочитать `pendingClose`: если `{ Drain: false }` — пакет отбросить (`continue`), иначе
      `Dispatch(item)` целиком; после каждого элемента — если `pendingClose != null`, вызвать
      `Complete(req)` и выйти из внутреннего цикла (design → Decision 4)
- [x] 2.5 `Complete(CloseRequest req)`: при `!Drain` — дочистить канал (`while TryRead`);
      `pendingClose = null`; `Disconnect?.Invoke(this)`; `req.Done?.TrySetResult()`.
      Порядок: `Dispatch` начатого пакета целиком → затем `Complete` (краевой случай закрыт)

## 3. Публичные методы

- [x] 3.1 `Close()`: под замком `ThreadSafeExec(true, …)` — таймер стоп, `device.Close()`
      (очередь НЕ чистить, `Disconnect` НЕ звать); затем `TryRequestClose(false)` →
      `queue.Writer.TryWrite(WAKE)`. Не ждать
- [x] 3.2 `FlushAndCloseAsync(ct)`: `insideHandler` → `InvalidOperationException`; под замком —
      таймер стоп, `device.Close()`, `wasOpen`; если `!wasOpen` — `return`; `TryRequestClose(true)`
      (если не прошёл — взять текущий `pendingClose`); `TryWrite(WAKE)`; `await req.Done.Task
      .WaitAsync(ct)`
- [x] 3.3 Отмена `FlushAndCloseAsync`: при `OperationCanceledException` перевести остаток
      в отбрасывание (`req.Drain = false`), пробросить; инвариант — остаток не доставляется,
      `Disconnect` всё равно происходит (design → Decision 6)
- [x] 3.4 `Open()`: под замком — `pendingClose = null`, `device.Open()`, таймер старт, `Connect`.
      Проверить: реоткрытие после `Close`/`FlushAndCloseAsync` работает
- [x] 3.5 `Dispose()`: `Close()` + `queue.Writer.TryComplete()` + `timer.Dispose()`. Диспетчер
      обработает запрос и выйдет по завершению канала; `Dispose` не ждёт `Disconnect`
- [x] 3.6 Сверить: `Close()` из обработчика теперь безопасен (публикует запрос, не ждёт);
      `FlushAndCloseAsync` из обработчика по-прежнему бросает

## 4. Сборка и существующие тесты

- [x] 4.1 `dotnet build -c Release` — 0 ошибок, 0 предупреждений
- [x] 4.2 `dotnet test` — прогнать; ожидаемо упадут тесты `CloseTests`, завязанные на синхронный
      `Disconnect` после `Close()`. Разобрать каждый: падение из-за смены контракта (ожидание
      правится) или регрессия (чинится код). Записать в отчёт
- [x] 4.3 Публичная поверхность — без изменений: сверить по диффу, ни одного нового/убранного
      public-члена; сигнатуры `Close`/`FlushAndCloseAsync`/`Dispose` те же

## 5. Правка и добавление тестов

- [x] 5.1 `CloseTests`: немедленное закрытие — ждать `Disconnect` через сигнал (TCS), не
      «сразу после `Close()`»; проверять «порт закрыт по возврату» отдельно от «`Disconnect`
      произошёл». Setup уже строит расстановку P1-в-обработчике/P2,P3-в-очереди
- [x] 5.2 `CloseTests`: новый тест — краевой случай исправлен: `Close()` во время обработчика
      `ReceiveData` пакета `Send_State` FMT 0 → `ReceivePowerUnitState` того же пакета доставлен
      **до** `Disconnect` (журнал: `data`, `state`, `disconnect`). Это зонд из
      `port-abstraction/report.md` § 6.1, теперь как постоянный тест
- [x] 5.3 `CloseTests`: новый тест — `Close()` из обработчика `ReceiveData` не бросает и не
      виснет; адаптер закрыт, `Disconnect` последний, после него событий нет
- [x] 5.4 `CloseTests`: `FlushAndCloseAsync` (доставка остатка, порядок, `Disconnect` после),
      отмена (остаток отброшен, `Disconnect` есть), из обработчика (исключение), пустая очередь,
      закрытый порт, `Dispose` с очередью — привести ожидания к асинхронному `Disconnect`
- [x] 5.5 Два параллельных `FlushAndCloseAsync` на одном адаптере: оба дожидаются, `Disconnect`
      один, событий после него нет (сегодня `drained` затирался — новый инвариант)

## 6. Стабильность

- [x] 6.1 `dotnet test` — все зелёные; прогнать 5 раз подряд, ни одного мигания; если мигает —
      сигнал вместо паузы или больше таймаут (не `Retry`), записать причину
- [x] 6.2 Отдельно прогнать `CloseTests` и `QueueTests` под нагрузкой (коллекция `adapter`
      без параллелизма уже есть) — убедиться, что пробуждение через `WAKE` не теряется

## 7. Проверка на живом адаптере

- [x] 7.1 **[совместно]** Регрессия на COM3: `on`/`off` (лампа, `Send_State` `[5,0,1,255]` /
      `[5,0,0,0]`), `state`, `state --fmt 200` (→ `StateFormatErrorData`), `listen` до PT-111 —
      порядок событий и `Disconnect` последним сохранены; `Close`/`Dispose` из DebugConsole
      не виснут

## 8. Документация и завершение

- [x] 8.1 `README.md` → «Закрытие»: `Disconnect` у `Close()`/`Dispose()` асинхронный (порт закрыт
      по возврату, событие вскоре, последнее); `FlushAndCloseAsync` дожидается; краевой случай
      про типизированное событие после `Disconnect` больше не актуален
- [x] 8.2 `openspec/config.yaml` → `context`: обновить описание закрытия (`Disconnect`
      асинхронный, один запрос `pendingClose` вместо трёх флагов), снять пометку о краевом случае
- [x] 8.3 Отчёт `report.md`: точка отсчёта (1.1), что удалено/добавлено (2–3), разбор упавших
      и правленых тестов (4.2, 5.x), исправленный краевой случай (5.2), стабильность (6.1),
      железо (7.1); отдельно — что не проверено и почему
