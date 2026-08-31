# Library modernization

The contemporary Library page is a presentation layer over Libation's existing library owners. It does not create a second library, search index, filter language, selection collection, or command implementation.

## Ownership and integration

`LibraryViewModel` requires the existing `ProductsDisplayViewModel`, shell-scoped `IFlightService`, `Configuration`, `ILibationCommandAdapter`, and `ResponsiveLayoutService`. The optional process command should be the already-created `CurrentFlightViewModel.ProcessCommand` so Library does not create another preflight/process path.

The shell integration shape is:

```csharp
var library = new LibraryViewModel(
    shell.Main.ProductsDisplay,
    shell.Flight,
    Configuration.Instance,
    shell.CommandAdapter,
    shell.Responsive,
    shell.CurrentFlight.ProcessCommand);
```

Host one `LibraryView` with that model as its `DataContext`. `LibraryView` exposes the same `LiberateClicked`, `LiberateSeriesClicked`, `ConvertToMp3Clicked`, and `TagsButtonClicked` event seams as the embedded `ProductsDisplay`, plus `SelectAndFocusSearch`, `InsertSearchTag`, `SetFilterHelpEnabled`, `SearchText`, and `CloseImageDisplay` for the existing window/search forwarding code. `DetailsProductsDisplay` is available when a host needs direct access to the unchanged grid control.

Overview or another route can call `LibraryViewModel.TryOpenBook(book)`. It resolves the stable product ID only in the current visible projection, opens/focuses it when present, and otherwise returns `false` without changing the user's filter.

Shell fan-in is complete: `AppShellViewModel` owns one `LibraryViewModel`, and
`AppShellView` hosts one `LibraryView` while forwarding the established
`ProductsDisplay` event seams back to `MainWindow`.

## One library and one selection

- `ProductsDisplayViewModel` remains the source list, search/filter language, filtered membership, and `DataGridCollectionView` owner.
- `VisibleLibraryEntriesChanged` publishes an immutable snapshot of the existing `LibraryBookEntry` objects. Gallery wraps those objects; it does not copy metadata ownership.
- Header filter text is debounced for 200 ms, within the plan's 175–225 ms starting range, then calls `ILibationCommandAdapter.ApplyFilterAsync`. Bad-query recovery, filter help, saved filters, no-result text, and trash matching remain in `MainVM`.
- Header sorting updates the existing `DataGridCollectionView`. The Gallery snapshot follows that view order, expanding a visible series parent into its matching episode entries and removing duplicate stable IDs.
- `FlightService` is the only selected-title collection. Gallery pointer/keyboard selection changes Flight by `AudibleProductId`; Details selection is projected to Flight; Flight changes are projected back to currently visible DataGrid rows.
- Replacing visible selection never removes selected IDs hidden by the active filter. `FlightService.SetVisibleItems` remains the hidden-count authority, and the header says exactly how many selected titles are hidden.
- A focused title is presentation state separate from Flight. The detail pane can stay pinned without preventing multi-selection.

## Details and command parity

Details mode embeds the production `ProductsDisplay` itself. Its columns, ratings, series rows, widths, order, visibility, sorting, resize/reorder behavior, cover/description interactions, and status controls are therefore unchanged.

The existing context-menu composition was extracted into one `ProductsDisplay` builder used by both DataGrid cells and Gallery cards. Gallery receives the same status, PDF, locate, remove, download, split-chapter, MP3, re-download, Audible removal, template, bookmark, and series commands. Gallery's copy command copies the visible Details columns for the selected books; the cell-specific copy command remains Details-only because Gallery has no clicked column. The discoverable **Choose columns** action opens the existing column chooser rather than creating another column preference model.

## Gallery virtualization and keyboard behavior

The current dependency graph has Avalonia's `VirtualizingStackPanel` but no `ItemsRepeater` or virtualizing wrap panel, and this tranche adds no dependency. Gallery therefore chunks the projection into responsive rows and hosts those rows in a `ListBox` backed by `VirtualizingStackPanel`. Only realized vertical rows instantiate their small, fixed number of `GalleryBookCard` controls. A 50,000-title source creates lightweight projection/row records but does not instantiate 50,000 cards or decode 50,000 covers.

The card control keeps native buttons and visible focus, adds literal selection text in addition to color/border state, and implements visual-order Left/Right/Up/Down movement. Space follows platform modifier selection, Enter opens details, Shift+F10 opens the shared context menu, double-click opens details, and macOS Control-click follows the existing context-menu convention.

## Cover lifecycle and bound

`CoverImageCache` is a presentation-only, typed `Bitmap` cache; Gallery never binds `GridEntry.Cover`.

- Disk/network acquisition runs away from the UI thread through the existing cancellable `PictureStorage.GetPictureSynchronously` API.
- Small cards request the existing 300-pixel source and decode to the rendered 180 logical-pixel cover width. The details pane requests the existing 500-pixel source and decodes to 300 logical pixels. Render scaling is included in decode width.
- Every realized card/pane owns a cancellation token and a reference-counted cache lease. Recycling or leaving the visual tree cancels the request and releases the lease.
- The byte budget is derived from actual realized small/medium cover consumers multiplied by their decode dimensions and four RGBA bytes per pixel. It is not an invented global item cap.
- Unreferenced entries are evicted least-recently-used until decoded bytes fit that viewport-derived budget. Active leases are never disposed underneath a control.
- Duplicate requests converge on one cached bitmap, race losers are disposed, cancellation after decoding disposes the unused result, and cache disposal deterministically disposes every bitmap.
- Missing/failed covers return `null` and use the semantic vector placeholder without layout shift.

The existing `PictureStorage` encoded-byte cache and downloader remain outside this presentation tranche. Gallery bounds decoded bitmap ownership; changing the legacy process-wide byte cache would cross the file-manager/domain boundary.

## Responsive and state behavior

`Configuration.LibraryViewMode` persists Details/Gallery with its existing backward-compatible Details default. No new view-mode setting exists.

The existing `ResponsiveLayoutService.ContextualPane` output controls the details surface: wide layout uses an inline right pane; other layouts use an overlay pane. The pane reads the focused `LibraryBookEntry` wrapper and never queries or persists another metadata model.

The page includes literal loading, empty-library, and no-result states. Empty actions route to the existing add-account, scan-library, and Trash commands. No-result copy and Trash hints come directly from `MainVM`, including its current trash-search behavior.

## Evidence boundary

This tranche was source-reviewed only as requested. It adds no fixtures or tests and does not claim the prompt's 10k/50k timing, memory, platform accessibility, or visual gates. Those remain fan-in/runtime evidence. The source-level proof is structural: a virtualizing row panel, viewport-realized cover leases, a byte-counted LRU, the existing DataGrid instance, stable-ID Flight selection, and one extracted context-command builder.
