## Context

Мотивация и отклонение от формулировки — `proposal.md`. Изменяемое требование —
`specs/packet-receiving/spec.md` (MODIFIED «Закрытие адаптера и судьба очереди»).

Текущая конструкция закрытия (`MTRFXXAdapter.cs` после `port-abstraction`):

- `Channel<byte[]>`, bounded, `DropWrite`, `SingleReader`; таймер под `lockObject` кладёт
  сырые 17 байт, диспетчер `DispatchAsync` вынимает и разбирает.
- Поля жизненного цикла: `closing` (volatile bool), `inFlight` (int, Volatile), `drained`
  (volatile TCS), `insideHandler` (AsyncLocal).
- `Close()` под замком: `closing = true`, таймер стоп, порт закрыт, `DiscardQueue()`, `Disconnect`.
- `FlushAndCloseAsync()` под замком: таймер стоп, порт закрыт, `drained = wait`; затем ждёт,
  если `Count > 0 || inFlight != 0`; в `finally` — `closing = true`, `Disconnect`.
- Диспетчер: `inFlight = 1` **до** `TryRead`, проверка `if (!closing) Dispatch`, `inFlight = 0`
  в `finally`, `drained?.TrySetResult()` после вычитывания до дна.

Инварианты живут в комментариях: «`inFlight` до `TryRead`, иначе окно», «сигнал после
обработчиков последнего пакета». Краевой случай: `closing` проверяется один раз перед
`Dispatch(bytes)` целиком, поэтому типизированное событие того же пакета проходит после
`Disconnect` (`port-abstraction/report.md` → «Находки», подтверждено зондом).

Страховка: 73 теста (`port-abstraction`), из них `CloseTests`, `QueueTests`, `ReceiveTests`
покрывают ровно эту область. Рефакторинг без них был бы неоправдан.

## Goals / Non-Goals

**Goals:**

- Отбрасывание/доставку остатка и вызов `Disconnect` выполняет диспетчер, не публичные методы.
- Убрать `inFlight`, `drained` и их межпоточные инварианты; свести `closing` к одной
  атомарной ссылке-запросу.
- `Disconnect` — последний по построению: обработчики начатого пакета доводятся до конца,
  затем `Disconnect`. Краевой случай исчезает.
- `Close()` не ждёт (асинхронный `Disconnect`); `Close()` из обработчика безопасен.
- Публичные сигнатуры не меняются.

**Non-Goals:**

- Смена сигнатур событий, async-потребление, событийное чтение — не трогаем.
- Синхронный `Disconnect` у `Close()` — решением владельца НЕ сохраняем.
- Изменение поведения `SendCommand`, разбора, шва.

## Decisions

### 1. Одна атомарная ссылка `pendingClose` вместо `closing` + `inFlight` + `drained`

```csharp
private sealed record CloseRequest(bool Drain, TaskCompletionSource Done);

// null — адаптер работает; не-null — опубликован запрос закрытия
private volatile CloseRequest pendingClose;
```

`Drain` — доставить остаток (`FlushAndCloseAsync`) или отбросить (`Close`/`Dispose`).
`Done` — TCS, который диспетчер завершает после `Disconnect` (для `FlushAndCloseAsync`);
для `Close`/`Dispose` — `null` (ждать некому).

**Почему ссылка, а не bool + отдельный TCS:** запрос атомарен целиком — диспетчер видит либо
полностью сформированный запрос, либо `null`. TCS едет внутри запроса, поэтому общего поля
`drained` нет: два параллельных `FlushAndCloseAsync` получают разные запросы и разные TCS,
а не затирают друг друга (сегодня — затирают).

**Почему `closing` не убирается полностью** (proposal → «Отклонение»): немедленное закрытие
обязано отбросить пакеты, стоящие в очереди **впереди** любого управляющего сообщения. При
`SingleReader` диспетчер решает «доставить/отбросить» до чтения сообщения — нужен внеполосный
признак. `pendingClose != null` и есть этот признак; он берёт на себя роль `closing`, но без
`inFlight`/`drained`.

### 2. Установка запроса — `Interlocked.CompareExchange`, первый выигрывает

```csharp
private bool TryRequestClose(bool drain, out CloseRequest req)
{
    req = new CloseRequest(drain, drain ? new TaskCompletionSource(
        TaskCreationOptions.RunContinuationsAsynchronously) : null);
    return Interlocked.CompareExchange(ref pendingClose, req, null) == null;
}
```

Если запрос уже стоит (повторный `Close`, или `Close` после `FlushAndCloseAsync`) — CAS
не проходит, второй вызов не создаёт второго закрытия. `Open()` сбрасывает `pendingClose = null`
под замком (реоткрытие).

### 3. Пробуждение диспетчера — сентинел в канал, доставка не обязательна

Диспетчер, спящий на `WaitToReadAsync` с пустой очередью, установкой поля не разбудится.
Поэтому после публикации запроса — `queue.Writer.TryWrite(WAKE)`, где `WAKE` — статический
`byte[]`-маркер:

```csharp
private static readonly byte[] WAKE = new byte[0];
```

- Если очередь пуста (диспетчер спит) — канал не полон, `TryWrite` проходит, диспетчер
  просыпается.
- Если очередь полна (диспетчер занят, `DropWrite` отбросит `WAKE`) — диспетчер и так в цикле
  чтения и увидит `pendingClose` сам.

Корректность не зависит от доставки `WAKE` — это только будильник. Диспетчер, вынув `WAKE`,
проверяет `ReferenceEquals(item, WAKE)` и пропускает (не разбирает как пакет).

### 4. Диспетчер владеет закрытием

```csharp
private async Task DispatchAsync()
{
    var reader = queue.Reader;

    while (await reader.WaitToReadAsync().ConfigureAwait(false))
    {
        while (reader.TryRead(out var item))
        {
            if (ReferenceEquals(item, WAKE)) { /* будильник */ }
            else
            {
                var close = pendingClose;
                if (close is { Drain: false }) continue; // немедленное: отбросить пакет
                Dispatch(item);                          // иначе доставить (и при Drain:true тоже)
            }

            var req = pendingClose;
            if (req != null) { Complete(req); break; }   // запрос виден — закрыть
        }
    }
}

private void Complete(CloseRequest req)
{
    if (!req.Drain)
        while (queue.Reader.TryRead(out _)) { }          // отбросить остаток
    // при Drain остаток уже доставлен выше (он был впереди запроса в FIFO)

    pendingClose = null;                                  // адаптер готов к реоткрытию
    Disconnect?.Invoke(this);                             // последнее событие
    req.Done?.TrySetResult();                             // разбудить FlushAndCloseAsync
}
```

Ключевое:

- **`Dispatch(item)` целиком** (общее + типизированные события) вызывается **до** проверки
  `pendingClose` на выход. Значит, пакет, начатый в момент закрытия, доводится до конца, и лишь
  потом `Disconnect`. Краевой случай закрыт по построению.
- **Немедленное** (`Drain: false`): пакеты, ещё не начатые, отбрасываются проверкой
  `close is { Drain: false } → continue`; `Complete` дочищает канал.
- **С обработкой остатка** (`Drain: true`): проверка на пропуск не срабатывает, всё до запроса
  доставляется (FIFO — запрос опубликован после того, как таймер перестал писать), затем
  `Complete` без сброса, `Disconnect`, `Done`.
- `Disconnect` — из потока диспетчера, между пакетами: ни один обработчик в этот момент не
  выполняется. «Последнее событие» верно без охраны флагом.

### 5. Публичные методы — закрыть порт под замком, опубликовать запрос

```csharp
public void Close()
{
    ThreadSafeExec(true, () =>
    {
        timer.Change(Timeout.Infinite, READING_INTERVAL);
        device.Close();
    });
    if (TryRequestClose(drain: false, out _)) queue.Writer.TryWrite(WAKE);
    // не ждём: Disconnect придёт из диспетчера
}

public async Task FlushAndCloseAsync(CancellationToken ct = default)
{
    if (insideHandler.Value)
        throw new InvalidOperationException("...диспетчер ждал бы сам себя...");

    var wasOpen = false;
    ThreadSafeExec(true, () =>
    {
        wasOpen = true;
        timer.Change(Timeout.Infinite, READING_INTERVAL);
        device.Close();
    });
    if (!wasOpen) return;

    if (!TryRequestClose(drain: true, out var req))
        req = pendingClose;                    // закрытие уже идёт — ждём его же
    queue.Writer.TryWrite(WAKE);

    try
    {
        await req.Done.Task.WaitAsync(ct).ConfigureAwait(false);
    }
    catch (OperationCanceledException)
    {
        // переключить на отбрасывание остатка: заменить запрос на Drain:false недостаточно
        // (диспетчер мог уже начать доставку) — достаточно снять drain для остатка.
        // деталь реализации ниже (Risks); Disconnect всё равно произойдёт
        throw;
    }
}

public void Dispose()
{
    Close();
    queue.Writer.TryComplete();   // диспетчер обработает запрос и выйдет по завершению канала
    timer.Dispose();
}
```

`Close()` больше не вызывает `Disconnect`, не чистит очередь, не ставит `closing`. `insideHandler`
остаётся — только как защита `FlushAndCloseAsync` от вызова из обработчика; `Close()` из
обработчика теперь безопасен (публикует запрос, не ждёт).

**`Open()`:** под замком — `pendingClose = null`, `device.Open()`, таймер старт, `Connect`.
Диспетчер живёт весь жизненный цикл, канал не завершается (кроме `Dispose`).

### 6. Отмена `FlushAndCloseAsync`

При отмене вызывающий перестаёт ждать `Done`, но диспетчер продолжит доставку остатка
(`Drain: true`) — это противоречит «остаток отбрасывается при отмене». Нужно при отмене
перевести уже опубликованный запрос в режим отбрасывания. Так как `CloseRequest` —
запись (immutable), отмена заменяет `Done`-ожидание, но не режим; поэтому режим drain хранится
как **изменяемое поле** запроса (не `record` с `init`, а класс с `volatile bool Drain`), и отмена
делает `req.Drain = false`. Диспетчер читает `req.Drain` в `Complete` и в проверке пропуска.
Гонка «диспетчер уже в `Dispatch(item)`» безвредна: этот пакет доигрывает, следующие
отбрасываются. Точная форма — при реализации; инвариант: после отмены остаток не доставляется,
`Disconnect` происходит.

> Уточнение к Decision 1: `CloseRequest` — **класс** с `volatile bool Drain` и `readonly TCS Done`,
> а не `record`, ради изменяемости `Drain` при отмене.

### 7. Что удаляется и что остаётся

| Было | Стало |
|---|---|
| `closing` (volatile bool) | роль перешла к `pendingClose != null` |
| `inFlight` (int) + инвариант «до `TryRead`» | удалён — не нужен |
| `drained` (volatile TCS) + «Count>0\|\|inFlight!=0» | TCS внутри `pendingClose`, ожидание по нему |
| `DiscardQueue()` из `Close()` под замком | `Complete()` в потоке диспетчера |
| `Disconnect` из `Close`/`Flush` | `Disconnect` из диспетчера |
| `insideHandler` | остаётся (только для `FlushAndCloseAsync`) |
| — | `pendingClose` (CloseRequest), `WAKE`-сентинел |

Итог по полям жизненного цикла: было `closing` + `inFlight` + `drained` + `insideHandler`
(4) → стало `pendingClose` + `insideHandler` (2), без межпоточных инвариантов.

## Risks / Trade-offs

- **`Disconnect` асинхронный — ломающее поведение.** Потребитель, считавший адаптер отключённым
  сразу после `Close()`, ошибётся. → Решение владельца; фиксируется в спеке, README, и проверяется
  у драйвера при интеграции. Тесты `CloseTests` переписываются на ожидание события через сигнал.
- **`Dispose` не ждёт `Disconnect`.** `using` завершится до события. → Осознанно (async
  `Disconnect`); кто хочет дождаться — `await FlushAndCloseAsync()`. То же, что и `Close()`.
- **Гонка отмены `FlushAndCloseAsync`** (Decision 6) — окно «диспетчер уже начал доставку остатка»;
  безвредно (один пакет доигрывает). Ревью + тест на отмену.
- **`WAKE`-сентинел в канале `byte[]`** — типобезопасность на `ReferenceEquals`, не на типе.
  Диспетчер обязан проверять маркер до разбора. → Один `if` в начале обработки элемента; тест
  «Close при пустой очереди → Disconnect» ловит потерю пробуждения.
- **Порядок `Error`/`Disconnect` при отказе порта** (`errorHandler: Close`): `Close` публикует
  запрос, `RaiseError` — сразу; `Error` придёт до `Disconnect` (сегодня — тоже до, но по другой
  причине). Наблюдаемо, спекой не фиксируется; тест не проверяет порядок этих двух.
- **Наименее покрытая часть переписывается.** → Есть 73 теста; после правки — те же зелёные плюс
  тесты на исправленный краевой случай и `Close()` из обработчика. Ревью диспетчера — внимательное.

## Migration Plan

Публичные сигнатуры не меняются — компиляция потребителей не ломается. Ломается допущение
«`Disconnect` до возврата `Close()`» — снимается подпиской на событие. Откат — `git revert`
одного change'а: возвращаются `closing`/`inFlight`/`drained` и синхронный `Disconnect`; тесты
`CloseTests` откатываются вместе с change'ем.

## Open Questions

- **Имя `pendingClose` / `CloseRequest`** — рабочее; не влияет на спеку и задачи.
- **Форма переключения drain при отмене** (Decision 6) — `volatile bool` в запросе или
  отдельный путь; решается при реализации, наблюдаемое поведение задано спекой («остаток
  отбрасывается, `Disconnect` происходит»).
