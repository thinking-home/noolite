> Шаги, помеченные **[совместно]**, требуют подключённого адаптера и участия владельца.
> Железо: COM3, реле SUF-1-300 (nooLite-F, ID 33347) на канале 0, датчик PT-111 на канале 1.

## 1. Точка отсчёта

- [x] 1.1 Дерево чистое, `dotnet build -c Release` и `dotnet test` зелёные (35 тестов, 0 предупреждений)
- [x] 1.2 Перечитать `MTRFXXAdapter.TimerCallback` / `TryRead` / `ThreadSafeExec` — зафиксировать
      в отчёте текущее поведение при исключении в обработчике (через `errorHandler: Close`
      адаптер **закрывается**)

## 2. Очередь и диспетчер

- [x] 2.1 Конструктор: параметр `int queueCapacity = 128`, валидация `< 1` →
      `ArgumentOutOfRangeException`; `Channel.CreateBounded<byte[]>` с `DropOldest`,
      `SingleReader = true`, `itemDropped` → `Interlocked.Increment(ref droppedPackets)`
- [x] 2.2 Свойство `DroppedPacketsCount` → `Volatile.Read(ref droppedPackets)`
- [x] 2.3 `TryRead`: вместо `Parse` и событий — `new byte[17]`, копия пакета, `writer.TryWrite`.
      Больше ничего под замком не делать
- [x] 2.4 Диспетчер: приватный `async Task DispatchAsync()`, запуск через `Task.Run` в конструкторе.
      Цикл: `WaitToReadAsync` → `TryRead` до дна → `Parse` → `ReceiveData` → ветвление
      на типизированные события (логика перенесена из `TryRead` без изменений в `Dispatch`)
- [x] 2.5 Изоляция: хелперы `Raise<T>` и `RaiseError` (design → Decision 4); `Parse` под `try`
      → `RaiseError` → `return` из `Dispatch`
- [x] 2.6 `AsyncLocal<bool> insideHandler`: `true` перед вызовом обработчиков пакета, `false`
      после — в `finally`

## 3. Закрытие

- [x] 3.1 Флаг `closing` (volatile bool): диспетчер проверяет его **после** вынимания пакета
      и **до** вызова обработчиков; если стоит — пакет отбрасывается. Иначе гонка «один пакет
      после `Disconnect`» (design → Risks, последний пункт)
- [x] 3.2 `Close()`: под замком — `closing = true`, таймер стоп, порт закрыт, очередь очищена
      (`while (reader.TryRead(out _))`), `Disconnect`. Канал **не** завершать — `Open` может
      быть вызван снова; при `Open` — `closing = false`
- [x] 3.3 `FlushAndCloseAsync(CancellationToken ct = default)`: проверка `insideHandler` →
      `InvalidOperationException`; под замком — таймер стоп, порт закрыт (очередь **не** очищать,
      `Disconnect` **не** звать, `closing` **не** ставить — иначе диспетчер отбросит остаток);
      ждать сигнала «очередь пуста» от диспетчера с `ct`; затем `closing = true`, `Disconnect`.
      При отмене — очистить очередь, `closing = true`, `Disconnect`, пробросить
- [x] 3.4 Механизм сигнала «пусто»: `TaskCompletionSource` в поле `drained`. **Уточнение
      против плана**: сигнал «после обработчиков, если `Count == 0`» недостаточен — между
      `TryRead` (Count → 0) и началом обработчиков `FlushAndCloseAsync` увидел бы «пусто»
      и вернулся бы посреди обработчика. Добавлен флаг `inFlight`, поднимаемый **до** вынимания
      и снимаемый после обработчиков всей пачки; `FlushAndCloseAsync` ждёт, если
      `Count > 0 || inFlight != 0`. Диспетчер сигналит после вычитывания очереди до дна
- [x] 3.5 `Dispose()`: `Close()` + `writer.TryComplete()` + `timer.Dispose()`. Диспетчер выйдет
      сам, дождавшись конца канала. `Dispose` не ждёт
- [x] 3.6 Проверить: `Open` после `Close` на том же адаптере работает — диспетчер один,
      канал не завершён, `closing` сброшен — режим `reopen`, оба раунда получают пакеты

## 4. Сборка и существующие тесты

- [x] 4.1 `dotnet build -c Release` — 0 ошибок, 0 новых предупреждений
- [x] 4.2 `dotnet test` — 35 зелёных (разбор от диспетчера не зависит)
- [x] 4.3 Публичная поверхность — только добавления: optional-параметр конструктора,
      `DroppedPacketsCount`, `FlushAndCloseAsync`, константа `DEFAULT_QUEUE_CAPACITY`.
      Сверено по диффу; старый вызов конструктора компилируется в Console и DebugConsole

## 5. DebugConsole — инструменты проверки

- [x] 5.1 Режим `slow <port> <channel> [--sleep MS] [--throw]`: обработчик `ReceiveData` спит `MS`
      (по умолчанию 3000); параллельный поток каждые 200 мс шлёт `on`/`off` попеременно
      с таймстампом; по Ctrl+C — отчёт: интервалы между отправками (мин/макс/avg), число
      отправленных, обработчиков, ошибок. `--throw` — каждый второй обработчик бросает
- [x] 5.2 Режим `flush <port> <channel> [--no-flush]`: обработчик спит 500 мс; 5 команд `on`
      подряд; затем `FlushAndCloseAsync` (или `Close` с `--no-flush`) с таймстампами —
      сколько обработчиков всего, сколько после `Disconnect`
- [x] 5.3 Режим `drop <port> <channel>`: адаптер с `queueCapacity: 2`, обработчик спит 2 с,
      10 команд `on` быстро → `DroppedPacketsCount`; `FlushAndCloseAsync` — диспетчер не завис
- [x] 5.4 Во всех режимах после `done` печатать `DroppedPacketsCount`, если > 0.
      Плюс режим `reopen` — для задачи 3.6

## 6. Проверка на живом адаптере

- [x] 6.1 **[совместно]** `slow COM3 0 --sleep 3000 --duration 12000` — 59 команд, интервал
      200–215 мс, ни одного проседания на 3 с; `Send_State` доходят по порядку
- [x] 6.2 **[совместно]** `flush COM3 0` — `FlushAndCloseAsync` вернулся через 1161 мс после
      обработки остатка; 0 обработчиков после `Disconnect`. (Из 5 команд блок ответил на 2 —
      сериализация адаптера, не диспетчер)
- [x] 6.3 **[совместно]** `flush COM3 0 --no-flush` — `Close()` вернулся за 108 мс, остаток
      отброшен (2 обработчика против 4), 0 после `Disconnect`
- [x] 6.4 **[совместно]** `drop COM3 0` — 8 отброшено при ёмкости 2, `FlushAndCloseAsync`
      вернулся — диспетчер не завис
- [x] 6.5 **[совместно]** Регрессия: `on`/`off`, `state --fmt 200`, `listen` до PT-111
      (`toggle: 58`, 29,6 °C / 41 %) — всё как в `read-state`, порядок сохранён
- [x] 6.6 **[совместно]** `slow COM3 0 --sleep 500 --throw --duration 5000` — 10 исключений →
      10 `Error`, типизированные события тех же пакетов доставлены, следующие обработаны,
      `port still open: True`

## 7. Документация и завершение

- [x] 7.1 `README.md`: раздел «Входящие пакеты» — подразделы «Как вызываются обработчики»
      и «Закрытие»
- [x] 7.2 Doc-комментарии на `Close()`, `FlushAndCloseAsync`, конструктор, `DroppedPacketsCount` —
      написаны при реализации
- [x] 7.3 Отчёт: результаты 4.1–4.3, 6.1–6.6 с таймстампами; явно — что не проверено без шва
- [x] 7.4 Решить с владельцем: режимы `slow`/`flush`/`drop`/`reopen` — **удалены** по решению
      владельца, DebugConsole откачен к master + печать `DroppedPacketsCount` после `done`.
      Результаты прогонов сохранены в `report.md`. Проверка диспетчера переезжает в юнит-тесты
      следующего change'а (точка расширения + тесты)
