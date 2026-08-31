# Contemporary UI architecture

The contemporary presentation is a reversible layer over existing Libation
owners. New contributors should start at the state owner, not at the screen.

```text
MainWindow
  ├─ current Avalonia content                     (flag off)
  └─ AppShellView + AppShellViewModel             (flag on)
       ├─ one NavigationService
       ├─ one FlightService
       ├─ one LibraryViewModel projection
       │    └─ existing ProductsDisplayViewModel and ProductsDisplay Details grid
       ├─ one DashboardViewModel projection
       └─ one ProcessingViewModel projection
            └─ existing ProcessQueueViewModel and ProcessQueueControl
```

## Ownership rules

| Concern | Authoritative owner | Contemporary responsibility |
|---|---|---|
| library data/filtering | `MainVM.ProductsDisplay` / `ProductsDisplayViewModel` | project visible entries; never build a second search index |
| Details behavior | existing `ProductsDisplay` | retain columns, sorting, context actions, series, and persistence |
| batch selection | shell-scoped `FlightService` | expose stable-ID selection to Gallery, Details, Overview, and Flight surfaces |
| processing | existing `MainVM.ProcessQueue` | project aggregate and item state; keep the legacy queue controls/log reachable |
| commands/dialogs | `MainVM` through `ILibationCommandAdapter` | route intent; never duplicate domain or confirmation logic |
| appearance | `ExperienceManager` | atomically resolve resources and composition from persisted preferences |
| navigation | `NavigationService` | own one route and one selected navigation item |

`MainWindow` creates the contemporary graph lazily and keeps it while the feature
flag is enabled or temporarily rolled back. Switching the flag changes the
window content, not user data. Closing the window disposes subscriptions,
commands, cache entries, and presentation wrappers.

## Feature flag and profile lifecycle

`Configuration.UseContemporaryShell` is backward-compatible and defaults to
`false`. With the flag off, `ExperienceManager` resolves the current Avalonia
appearance and `MainWindow` hosts its original content. With the flag on,
`Configuration.ExperienceStyle` resolves Cellar, Tasting Room, Follow System,
or High Contrast.

Candidate resource dictionaries are created and validated before replacing the
active dictionary. A failed profile load retains the committed presentation and
falls back through High Contrast to the current interface. Preview scopes own
isolated resources and do not mutate the application.

Contemporary persisted values repair only their invalid entry, log the recovery,
and disable `UseContemporaryShell` when conversion fails so corrupted settings
cannot strand startup inside a partially resolved shell. Enabling the shell is
persisted after the other profile choices.

## Routes and responsive behavior

`AppRouteId` is the only contemporary route identifier. `NavigationService`
persists the last compatible destination and exposes primary and utility items.
`ResponsiveLayoutService` derives rail, contextual-pane, and queue presentation
from the effective window size and current profile. Feature views bind to those
results; they do not invent their own breakpoints.

The current macOS quick-filter gestures retain `Cmd+1…0`. Contemporary route
shortcuts therefore use `Cmd/Ctrl+Shift+1…5` for the five primary destinations.
`F6` cycles the live shell regions, `Cmd/Ctrl+Enter` invokes the shared Flight
preflight/process command, and Escape closes the active navigation/Flight/
Decanter transient surface with focus restored to its opener. Native menus
remain authoritative for commands outside those routes; the guided-tour command
routes to contemporary onboarding while the contemporary shell is active.

## Library and Current Flight

`LibraryViewModel` consumes immutable snapshots from the existing filtered
collection. Details mode hosts the existing `ProductsDisplay`; Gallery uses
recycled rows and lazily acquired cover leases. The cover cache is bounded by
the current realized viewport and open details cover, evicts unleased entries
least-recently-used, and disposes decoded bitmaps.

The Flight stores stable Audible product IDs and retains selected titles when a
filter hides them. Details and Gallery publish selection changes into that same
service. Remove and Clear create undo tokens. Processing uses a preflight result
and the existing `MainVM.QueueBooksAsync` path; warnings require a second,
explicit activation, while blockers never enqueue.

## Processing

`ProcessingViewModel` wraps the existing `ProcessQueueViewModel`. It maintains
stable presentation wrappers for active, waiting, completed, failed, and
cancelled items, and coalesces membership changes onto the UI dispatcher. The
legacy `ProcessQueueControl` remains available under Queue controls & log for
advanced settings, log copy/clear, and established recovery behavior. Only one
live legacy queue control is attached at a time.

## Adding a screen or component

1. Add a route only to `AppRoute`/`NavigationService`; do not add a competing
   navigation enum or selected-state property.
2. Obtain domain state through the existing owner or a narrow adapter.
3. Use `Libation.*` semantic resources with `DynamicResource`; palette hex values
   belong only in `DesignSystem/Palettes`.
4. Reuse components under `DesignSystem/Components` and assets by stable ID.
5. Include literal loading, empty, warning/error, success, and recovery copy
   required by the feature.
6. Dispose subscriptions, commands, leases, and caches at the owning lifecycle.
7. Record any unverified runtime or platform behavior instead of inferring it
   from a build.
