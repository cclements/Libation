# Overview visual mismatch and integration notes

Prompt 06 is implemented as two compositions over one `DashboardViewModel`. The
views do not create their own view models, query the database, or own a second
selection/queue. `AppShellViewModel` constructs one dashboard instance, and
`AppShellView` assigns that same instance to both `CellarOverviewView` and
`TastingRoomOverviewView`, so profile switching preserves live state.

## Data and action ownership

- `MainDashboardDataSource` takes immutable snapshots from `MainVM.LibraryStats`,
  `ProductsDisplay.GetVisibleBookEntries()`, `ProcessQueue.Queue`, and the shared
  `IFlightService`. Library projection and sorting run on a worker task. The library
  projection is cached while only queue progress changes.
- Existing `LibraryStats` counts remain authoritative. The overview does not repeat
  the filesystem-aware `LibraryCommands.GetCounts` rules or open a database context.
- Add account, scan, locate, and search actions use `ILibationCommandAdapter`.
  Current Flight processing uses `FlightPreflight` and `IFlightProcessAdapter` with
  the existing queue. Cancelling the Decanter uses `ProcessQueue.CancelAllAsync()`.
- `IDashboardNavigation` is the explicit shell seam for opening a real book, Library,
  or Processing route. The overview does not import route strings or window policy.
- `IDashboardSupplementSource` is the asynchronous seam for facts that have no
  authoritative `MainVM` owner: connectivity, stale-scan policy and timestamp, total
  local storage, calculable storage savings, and application-update state. Missing
  facts render as “Not available” or “Last scan time is not available”; they are not
  estimated from unrelated fields.

The intended host construction is equivalent to:

```csharp
var dashboard = new DashboardViewModel(
    commandAdapter,
    flightService,
    flightProcessAdapter,
    dashboardNavigation,
    optionalSupplementSource);

cellarView.DataContext = dashboard;
tastingRoomView.DataContext = dashboard;
```

The host owns the dashboard lifetime and must dispose it with the shell.

## State coverage

| State | Source of truth | Presentation in both profiles |
|---|---|---|
| Initial loading | `LibraryStats == null` | Indeterminate, live-region loading panel; no zero-count claim |
| Zero library, no account | ready stats + `AccountsCount == 0` | “Connect an Audible account” with the existing add-account command |
| Account ready for a scan | ready zero stats + `AccountsCount > 0` | “Your cellar is ready” with the existing scan command |
| Empty library while scanning | `ActivelyScanning` + zero titles | Literal scanning state and current `ScanningText` |
| Catalogued, no open local copies | titles present + completed count zero | “Your books are catalogued” and an Open Library action |
| Stale scan | supplement provider says `Stale` | Warning and Scan Library action; the overview invents no age threshold |
| Offline | supplement provider says `Offline` | Warning that local library data remains available, plus Refresh |
| Refresh/action/provider error | caught exception or supplement error | Literal danger banner; prior good snapshot remains intact |
| No active work | no queued/working queue items | Decanter says “No active processing work” and “Queue is idle” |
| Active work | existing queued/working items | Live aggregate progress, current title, expandable shared `QueueItem` rows, cancel-all |
| Failed work | existing queue error count/items | Danger banner and shell-routed Open Processing action |
| Empty Current Flight | shared flight count zero | Shared `FlightTray` with a literal selection instruction |
| Active Current Flight | shared flight items | Actual title/author/duration data, preflight warnings, process and clear actions |

No fake covers, sample books, personalized greetings, storage numbers, scan dates, or
update claims are present. Overview book components intentionally receive no cover
bitmap and use their semantic placeholder; the production Library Gallery separately
loads real covers through its bounded `CoverImageCache`.

## Shared component use

| Shared component | Prompt 06 use |
|---|---|
| `PageHeader` | Both profile headers, account status, literal locate action, code-native hero art |
| `MetricCard` | Cellar compact five-card set; Tasting Room ordered four-card set |
| `BookRow` | Virtualized library, recent-addition, and Current Flight rows |
| `StatusBadge` | Account/scan health and nested book/queue status |
| `DropZone` | Existing locate-files command card in both profiles |
| `AttentionBanner` | scan, offline, stale, failed-job, and error states |
| `FlightTray` | Persistent right tray in wide Cellar; dashboard card in Tasting Room |
| `DecanterSummary` | Bottom Cellar dock and peer Tasting Room dashboard card |
| `QueueItem` | Expanded active-work details in both Decanters |
| `EmptyState` | Account, scan, scanning, and no-local-copy states with contract illustration templates |
| `BookCard` | Not used here; the gallery and real-cover loading contract belong to Prompt 07 |
| `LibationNavigationRail` | Shell-owned and intentionally not nested inside a route view |
| `ToastHost` | Not used; persistent overview failures require a banner rather than a transient toast |
| `ThemePreviewCard` | Settings/profile-preview component, not overview content |

All profile-dependent colors, type, spacing, borders, and density come from
`Libation.*` semantic resources. Immutable glyphs and templates use these asset IDs:

- `glyph.library`, `glyph.downloads`, `glyph.processing`, `glyph.completed`, and
  `glyph.metadata`;
- `illustration.cellar.add-books`, `illustration.cellar.empty-library`, and
  `illustration.cellar.empty-decanter`;
- `illustration.tasting-room.add-books` and
  `illustration.tasting-room.empty-decanter`;
- `illustration.shared.account-connection`.

## Intentional visual differences from the boards

- Cellar is library-first and denser: search and metrics lead into a virtualized
  library list, a persistent wide Current Flight tray, and a full-width Decanter.
  It does not reproduce the board's fake cover gallery; Prompt 07 owns the real
  Gallery/Details library implementation.
- Outside Cellar's wide Overview composition, the shell owns the responsive Flight
  pane or overlay. Both bind the same `CurrentFlightViewModel`; changing route or
  profile does not create another selection owner.
- Tasting Room is spacious and overview-first: four metrics, attention, peer Flight
  and Decanter cards, recent additions, a locate-files card, and account/scan/storage
  facts. It uses a neutral welcome line and no personal/location data.
- Narrow Tasting Room hides hero decoration and stacks the primary and lower rows.
  The four layout classes and thresholds are the ones specified in plan section 8;
  reflow never replaces the shared view model.
- The decorative bottle-rack motif, still-life motif, and optional grain remain
  deferred under the Prompt 03 asset contract. Their absence does not remove a
  workflow or status.
- `DropZone` currently exposes a browse command but no dropped-payload command. The
  overview therefore describes and wires the existing file locator without claiming
  that a dropped payload is imported. A future component API may close that gap.
- The queue exposes cancel-all but no established pause-all command, so Pause is not
  shown. No synthetic pause state is presented.

## Integration result and evidence boundary

1. Shell fan-in is complete: `ShellDashboardNavigation` implements
   `IDashboardNavigation`; both profile views receive the shell's shared Dashboard,
   Flight, command, Library, and Processing owners; `AppShellViewModel.Dispose`
   releases the dashboard subscriptions.
2. Stale/offline/storage-saved/update states are fully renderable but cannot make a
   live claim until an authoritative `IDashboardSupplementSource` is supplied.
3. Release compilation proves the current source/XAML target only. Visual screenshots,
   keyboard traversal, screen-reader output, supported-platform behavior, and
   automated interaction remain unverified evidence gates.
