# The Decanter integration

The contemporary Processing destination is a presentation projection over the
existing `LibationUiBase.ProcessQueue.ProcessQueueViewModel`. It does not create,
persist, dispatch, pause, retry, or otherwise own a second queue.

## Ownership

| Concern | Authoritative owner | Contemporary seam |
|---|---|---|
| Queue order, active work, completed work | `TrackedQueue<ProcessBookViewModel>` | `ProcessingViewModel` keeps stable wrappers and groups them by current status. |
| Execution and concurrency | `ProcessQueueViewModel` | The source instance is passed through unchanged. |
| Per-title progress and cancellation | `ProcessBookViewModel` | `ProcessingQueueItemViewModel` forwards observable state and calls `CancelAsync`; queued cancellation also removes the same queued item. |
| Applicable retry | `MainVM.QueueBooksAsync` and the existing queue owner | Failed download/decrypt rows may submit the same book and effective configuration again; operations without a retained recipe do not guess. |
| Performance settings | `ProcessQueueViewModel` | The existing validated controls remain in **Queue controls & log**. |
| Queue log | `ProcessQueueViewModel.LogEntries` | Failed rows select both the outer workspace and the inner **Queue Log** tab; existing copy and clear behavior remains there. |

`ProcessingViewModel` coalesces membership changes onto the next Avalonia UI
dispatcher pass. Progress stays item-local, so a progress tick does not rebuild
every group or instantiate a second queue. Active, Waiting, Completed, and
Failed / cancelled use virtualizing list controls.

## Status and recovery

- `Working` maps to Processing.
- `Queued` maps to Download Pending while retaining the literal queue status.
- successful completion maps to Completed.
- failure and cancellation remain separate semantic states.
- each queue item has one correlation ID shared by its structured log scope,
  retained queue-log lines, visible failure summary, and scrubbed copied detail;
- failed download/decrypt rows expose retry through the established queue owner;
  other operation types omit retry rather than guessing at consumed state;
- failed and cancelled rows expose **Open log**, which selects the actual inner
  Queue Log tab, plus scrubbed **Copy technical details**.

The legacy queue controls remain reachable because they own queue positioning,
concurrency, speed limiting, auto-scroll, log copy/clear, and the exact existing
cancel/clear behavior. Their static event handlers now attach and detach with the
visual tree so switching shell presentation cannot retain a stale subscriber. A
future presentation may replace individual controls
only after an equivalent public queue adapter exists.

## Motion and decoration

The page adds no timers or animations. Reduced motion therefore needs no
alternate execution path. Functional copy, controls, progress, status text, and
focus order remain when decorative imagery is disabled; empty-state imagery is
supplemental.

## Evidence boundary

Release compilation is the current local proof. Runtime queue operation,
assistive-technology behavior, large-queue performance, and cross-platform
rendering require separately authorized evidence and are not claimed here.
