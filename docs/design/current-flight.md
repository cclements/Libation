# Current Flight integration

Current Flight is the shell-scoped ordered set of stable Audible product IDs
that the user explicitly chooses for a batch action. `FlightService` is the
single selection source for Details, Gallery, route changes, and profile
changes; UI controls never maintain a parallel selection list.

## State and identity

- `FlightItemId` is derived from `AudibleProductId` and rejects missing IDs.
- Add and range-add prevent duplicates.
- Library reconciliation updates current book objects and prunes removed IDs.
- Filtering changes only `HiddenCount`; hidden titles remain selected and are
  named in the tray warning.
- Session persistence is opt-in through `PersistFlightBetweenSessions` and is
  false by default. When disabled, no Flight IDs are written.
- Removing one title and clearing the Flight return an undo token. The
  presentation exposes that token through a literal Undo notification.

## Presentation

`CurrentFlightViewModel` projects the shared service into one reusable view:

- Cellar uses a persistent right tray at the wide layout breakpoint.
- Other layouts open the same view model in an overlaid drawer, preventing the
  primary text region from being squeezed.
- Tasting Room can also bind the same presentation state from its overview
  composition.
- Profile and route changes do not recreate `FlightService`.
- Output profiles use an ephemeral configuration copy and therefore do not
  overwrite saved Download/Decrypt settings.

## Processing intent and preflight

The process action calls `FlightPreflight.Evaluate` and then the existing
`MainVM.QueueBooksAsync` / `ProcessQueueViewModel` path through
`FlightProcessAdapter`.

- Blocking issues never queue work and state the required correction.
- When warnings exist, the first activation presents them and changes the
  action to **Process anyway**. A second explicit activation is required for
  the unchanged selection and output profile.
- A selection or output-profile change invalidates that confirmation.
- A clean preflight submits immediately because it contains no destructive or
  ambiguous choice.
- The Flight remains selected after submission; the queue is authoritative and
  subsequent preflight warns about titles that already have active work.

Account authorization, free-space estimation, and format compatibility are
only reported when reliable existing APIs expose them. The current first pass
blocks missing output location and unavailable titles and warns about active or
already-complete titles; it does not invent unavailable domain evidence.

## Evidence boundary

Source review and Release compilation can establish ownership and integration.
Keyboard/screen-reader runtime behavior, warning confirmation, queue execution,
and cross-platform drawer rendering require separately authorized evidence and
are not claimed from compilation alone.
