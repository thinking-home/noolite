# Отчёт: dispatcher-lifecycle

## 1. Точка отсчёта

`dotnet build -c Release` — 0 ошибок, 0 предупреждений. `dotnet test` — 73 пройдено.
Исходное состояние: `closing` (volatile bool), `inFlight` (int), `drained` (volatile TCS),
`insideHandler` (AsyncLocal); `Close`/`FlushAndCloseAsync` сами чистили очередь и звали
`Disconnect` синхронно. Краевой случай (типизированное событие после `Disconnect`) подтверждён
зондом в `port-abstraction`.

## 2. Что удалено и что добавлено

**Удалено:** поля `closing`, `inFlight`, `drained`; метод `DiscardQueue` оставлен (используется
`Complete`); межпоточные инварианты «`inFlight` до `TryRead`», «`Count>0||inFlight!=0`»,
обнуление `drained` в `finally`.

**Добавлено:**

- `CloseRequest` — приватный вложенный класс: `volatile bool Drain`, `readonly TaskCompletionSource
  Done`. Поле `CloseRequest pendingClose` (одно вместо трёх). Сентинел `WAKE = new byte[0]`.
- `RequestClose(bool drain)` — `Interlocked.CompareExchange` публикует запрос, первый выигрывает.
- `Complete(CloseRequest req)` — снимает **свой** запрос через CAS (реоткрытие-безопасно),
  при `!Drain` дочищает очередь, вызывает `Disconnect`, завершает `Done`.
- `RaiseDisconnect()` — `Disconnect` из потока диспетчера, исключение обработчика уводится
  в `Error` (иначе убило бы диспетчер).

**Диспетчер** (`DispatchAsync`): для каждого элемента — `WAKE` пропускается по `ReferenceEquals`;
если `pendingClose is { Drain: false }` — пакет отбрасывается и вызывается `Complete` (немедленное
закрытие), иначе `Dispatch(item)` целиком; после опустошения очереди `Complete` для оставшегося
запроса (доставка остатка/отмена). `Dispatch` начатого пакета всегда завершается **до** проверки
на закрытие — краевой случай закрыт по построению.

**Публичные методы:** `Close`/`FlushAndCloseAsync`/`Dispose` под замком только закрывают порт
и стоп-таймер, затем `RequestClose` + `TryWrite(WAKE)`. `Close`/`Dispose` не ждут. `Open`
сбрасывает `pendingClose` под замком.

Итог по полям жизненного цикла: `closing`+`inFlight`+`drained`+`insideHandler` (4) →
`pendingClose`+`insideHandler` (2), без межпоточных инвариантов.

### Отклонение от формулировки (зафиксировано в proposal/design)

`closing` полностью убрать нельзя — немедленное закрытие обязано отбросить пакеты, стоящие
в очереди впереди управляющего сообщения, а при одном читателе это требует внеполосного признака.
Роль `closing` взяла `pendingClose != null`; три поля свелись к одному.

### Правки сверх плана — по ходу реализации

1. **CAS-защита `Complete`** (не было в псевдокоде Decision 4). Диспетчер захватывает `req`
   в локальную переменную; без CAS устаревший `Close` после реоткрытия (`Open` сбросил
   `pendingClose`, пришёл новый пакет) мог бы вызвать `DiscardQueue` и отбросить этот пакет.
   `Complete` снимает именно свой запрос (`CompareExchange(pendingClose, null, req) == req`),
   иначе выходит без действий. Реоткрытие-безопасно.
2. **`RaiseDisconnect` оборачивает `Disconnect`.** Теперь он вызывается из потока диспетчера;
   исключение обработчика убило бы диспетчер (unobserved Task). Уводим в `Error`, как обработчики
   пакетов. Прежде `Disconnect` звался внутри `ThreadSafeExec`, где исключение тоже уходило
   в `Error` — поведение сохранено, дисциплина изоляции распространена на новый путь.

## 3. Сборка и существующие тесты (4.1–4.3)

Сборка Release — 0/0. `dotnet test` после правки кода — **4 падения**, все — смена контракта:

| Тест | Причина падения | Разбор |
|---|---|---|
| `LifecycleTests.Close_RaisesDisconnect_AndClosesPort` | `disconnects == 1` сразу после `Close()` | контракт: `Disconnect` теперь асинхронный — ждать через сигнал |
| `CloseTests.Close_WithPendingPackets…` | timeout на `Disconnect` до отпускания `Gate` | контракт: `Disconnect` после доигровки начатого пакета — отпускать `Gate` до ожидания |
| `CloseTests.FlushAndClose_Cancelled…` | то же | то же |
| `CloseTests.Dispose_WithPendingPackets…` | то же | то же |

Ни одного падения из-за регрессии кода — все четыре теста завязаны на прежний синхронный
`Disconnect`. Ожидания приведены к асинхронному контракту (задачи 5.1, 5.4): «порт закрыт
по возврату» проверяется отдельно от «`Disconnect` произошёл», событие ждётся через сигнал,
`Gate` отпускается перед ожиданием `Disconnect`.

Публичная поверхность (4.3): по диффу новые `public` — только внутри `private sealed class
CloseRequest` (вложенный приватный класс, наружу не виден). Сигнатуры `Close`/`FlushAndCloseAsync`
/`Dispose` те же. Публичное API адаптера не изменилось.

## 4. Новые тесты (5.2, 5.3, 5.5)

- `Close_DuringHandler_TypedEventDeliveredBeforeDisconnect` — **исправленный краевой случай**:
  `Close()` во время обработчика `Send_State` FMT 0 → журнал `data, state, disconnect`
  (типизированное событие ДО `Disconnect`). Прежний зонд из `port-abstraction` давал
  `data, disconnect, state`.
- `Close_FromHandler_DoesNotThrowOrHang` — `Close()` из обработчика `ReceiveData` не бросает
  и не виснет; P2 отброшен, `Disconnect` последний.
- `SecondFlushWhileDraining_ReturnsEarly_DisconnectOnce` — второй `FlushAndCloseAsync`, пока
  первый дренирует, возвращается сразу (порт уже закрыт, ветка `wasOpen == false`); первый
  доставляет остаток; `Disconnect` ровно один раз. (Формулировка задачи 5.5 «оба дожидаются»
  не соответствует коду: второй вызов короткозамыкается на `wasOpen`, что есть и в исходном
  коде; тест приведён к фактическому корректному поведению.)

`dotnet test` — **76 пройдено** (было 73, +3).

## 5. Стабильность (6.1–6.2)

`dotnet build -c Release` — 0 предупреждений. `dotnet test` ×5 подряд — 5 × 76 зелёных, ни одного
мигания. Отдельно `CloseTests`+`QueueTests` ×3 — 3 × 12 зелёных: пробуждение через `WAKE`
не теряется.

## 6. Проверка на живом адаптере (7.1)

COM3, SUF-1-300 (ID 33347) на канале 0, PT-111 на канале 1. DebugConsole Release.

| Команда | Результат |
|---|---|
| `on COM3 0 -f` | `Send_State` `[5,0,1,255]` → `state: On, power 255`, лампа включилась; `done`, затем `disconnect` (асинхронный, не виснет) |
| `off COM3 0 -f` | `Send_State` `[5,0,0,0]` → `state: Off, power 0`, лампа выключилась |
| `state COM3 0` | `Off` |
| `state COM3 0 --fmt 200` | `StateFormatErrorData` («unknown state table row requested») |
| `listen COM3` | 7 пакетов PT-111 (`RX / MicroclimateData / FMT 7 / канал 1`, toggle 20, 21, 24…); каждый — `data:` затем `microclimate:` в порядке прихода (например `[173,33,24,255]` → 42,9 °C / 24 %) |

Порядок событий сохранён; `disconnect` печатается последним (теперь после `done` — асинхронный
`Disconnect` из потока доставки), `Close`/`Dispose` не виснут.

## 7. Что не проверено

- **Гонка отмены `FlushAndCloseAsync` посреди доставки остатка** (design → Decision 6) —
  окно микросекундное; покрыто тестом `FlushAndClose_Cancelled` на уровне «остаток отброшен,
  `Disconnect` есть», но не сам момент переключения `Drain`. Ревень + логика.
- **Реоткрытие в гонке со стёртым запросом** (CAS-защита `Complete`) — рассуждение в разделе 2;
  тест `Reopen_DeliversPacketsAgain` проходит, но конкретное окно не форсируется. Ревью.
