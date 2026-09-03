# Contemporary secondary destinations

S6 implements Downloads, History, Accounts, Settings, Tools, Trash, and the five-step onboarding surface as presentation over Libation's established owners. It does not introduce another downloader, queue, Flight, account store, settings writer, library filter, deletion path, or route dispatcher.

The contemporary shell and current Libation interface remain two presentations over the same application state. Native menus, existing dialogs, established keyboard gestures, and Classic-mode behavior remain available during the default-off rollout.

## Construction and lifetime

`AppShellViewModel` owns one instance of each destination view model for the shell lifetime:

```csharp
Downloads = new DownloadsViewModel(CommandAdapter);
History = new HistoryViewModel(main);
Accounts = new AccountsViewModel(CommandAdapter);
Settings = new SettingsViewModel(CommandAdapter, configuration);
Tools = new ToolsViewModel(CommandAdapter);
Trash = new TrashViewModel(CommandAdapter);
```

Hidden routes retain their view models so searches, row identity, live state, and in-flight owner actions do not restart on navigation. Refreshes reconcile existing observable rows by stable title or presentation identity. `SecondaryDestinationViewModel` serializes delegated actions, publishes literal local errors, logs exceptions, and disposes only commands it created.

## Destination contracts

| Destination | Authoritative input | Production presentation | Delegated actions |
|---|---|---|---|
| Downloads | `MainVM.LibraryStats`, existing process queue, known local source paths | one virtualized list with stable Download Pending, Downloading, Downloaded, and Unavailable sections | title Download/Process/Retry when applicable, title Locate, pending books/PDFs, locate files, convert M4B, refresh |
| History | `LibraryBook.DateAdded`, `LastDownloaded`, retained queue log | timestamped typed outcomes with date/action/result/text filters; limited-history disclosure | refresh only |
| Accounts | `AccountPresentationSource` safe snapshots over account settings and current library facts | masked or genuine nickname, known marketplaces/title count, local credential state, scan inclusion | add/manage, per-account scan, edit marketplaces, forced interactive reauthentication, cancel-default removal |
| Settings | `Configuration`, five established dialog sections | native contemporary appearance draft plus searchable cards for the five real tabs | atomic apply/reset, onboarding re-entry, exact tab deep links, Accounts and About |
| Tools | existing `MainVM` commands and current Library filter | literal scope, consequence, risk, live startup-filter preference, owner-provided update state | established maintenance, discovery, metadata, quality, export/filter, About, and Hangover owners |
| Trash | `DbContexts.GetDeletedLibraryBooks()` | one searchable, virtualized list containing only actionable deleted non-parent records | restore and cancel-default permanent record deletion |

All profile-dependent color, typography, spacing, decoration, and status treatment comes from `Libation.*` resources. The views reuse `PageHeader`, `BookRow`, `StatusBadge`, `AttentionBanner`, `EmptyState`, and the scoped profile-preview components.

## Truth and privacy boundaries

### Downloads

Each row joins one library title with its existing queue item. Membership changes when the real queue stage or retained library status changes; reconciliation preserves surviving book and section-row instances so a refresh does not discard focus merely to update counts.

Rows publish only known facts:

- masked account and recorded marketplace;
- stored download quality when present;
- the byte size of an existing Audible or processed source file when present;
- queued/working progress from the current queue item; and
- an action only when its owner predicate is satisfied.

Unknown expected sizes remain absent. An unavailable remote title with a retained local copy remains Downloaded. Retry is offered only for a failed, reconstructible download/decrypt stage whose book or PDF still needs work. Locate uses the same extracted owner path as the current interface.

### History

History is an explicitly limited projection, not a durable audit log. It can show catalogue-added time, last completed download time, and retained current-session queue messages when those sources exist. It does not imply durable scan, export, tag, account, restore, or deletion history.

Correlation IDs remain typed diagnostic data. They are removed from visible detail and are not searchable public prose. Refresh captures input on the UI thread, computes projection away from it, coalesces requests, and reconciles one stable `ObservableCollection` on return. The single list owns its `VirtualizingStackPanel`; no ancestor item scroller defeats virtualization.

### Accounts

`AccountPresentationSource` may inspect account persistence, but only immutable `AccountPresentationSnapshot` values cross into the view model. A snapshot contains an opaque presentation ID, safe display name, known marketplace names, locally counted titles, coarse stored-credential state, scan inclusion, and action availability. Raw account IDs, credentials, cookies, tokens, activation bytes, and domain `Account` instances do not cross the boundary.

Generated account names are masked; a genuine nickname remains readable with any embedded login masked. Authorization labels describe local stored state only and never claim that Audible has accepted it remotely.

Per-account actions resolve the opaque presentation ID back inside the source and delegate to established owners. Reauthenticate forces the real interactive login path. Edit and Remove open the transactional `AccountsDialog` at the target account. Removal defaults to cancel and explains that saved sign-in/marketplace settings are deleted while existing Library records and local audiobook files remain.

### Settings, About, and updates

The route indexes exactly `Important Settings`, `Import Library`, `Download / Decrypt`, `Audio File Settings`, and `Audiobookshelf`. Each card opens the established `SettingsDialog` with that exact tab selected; validation, directory handling, token conversion, and dialog save/cancel behavior stay there.

Contemporary appearance is native to the route. One draft includes profile or High Contrast choice, density, decoration, motion, default Library view, navigation rail, system typography, Decanter visibility, and Flight persistence. Preview changes do not mutate the active shell. Apply writes one `ContemporaryExperienceSettings` transaction. Reset writes the supported defaults while retaining the contemporary shell.

The current-interface theme editor remains reachable from Important Settings. About/version/release notes/update checking remain owned by the established About dialog and are reachable through Settings, Tools, the About route, and preserved menus.

### Tools

Commands that say `visible titles` use the complete shared Library filter result, not only realized rows in the viewport. Every card states its consequence and one of the typed risk classes: read-only, needs review, changes data, destructive, or external.

The startup-filter switch reads and updates the live owner preference. Process visible titles calls an awaitable owner seam, calculates the exact eligible scope—including PDF-only work—and presents a cancel-default confirmation before any queue mutation. The route deliberately does not display the legacy book-only count as though it were the confirmation total.

### Trash

Trash is no longer a gateway to a second list. It projects the existing deleted-record source inline and excludes retained or series-parent rows from selection. Search and refresh reconcile stable row instances.

Restore delegates directly to the existing restoration owner. Permanent deletion always asks for cancel-default confirmation before the established database-record deletion path. The copy states the material boundary: the selected Libation records are removed; existing audiobook files are not deleted from disk.

## Command parity

| Workflow | Contemporary entry | Existing owner retained |
|---|---|---|
| Add/manage account | Accounts; Settings; onboarding | `MainVM.AddAccountsAsync` / `ShowAccountsAsync` |
| Scan one account | account card | existing account-scan path through `ApiExtended` and `MainVM` |
| Edit marketplaces/remove account | account card | targeted transactional `AccountsDialog` |
| Force interactive reauthentication | account card | established login owner with interactive auth forced |
| Download/process/retry one title | Downloads row | existing queue and processing commands |
| Locate one or many files | Downloads; Tools; onboarding | extracted `MainVM` locate owner |
| Download pending books/PDFs | Downloads | existing backup commands |
| Convert M4B library | Downloads | existing conversion owner and confirmation |
| Process visible titles | Tools | awaitable `MainVM` owner and confirmation |
| Detect/set visible status | Tools | existing status workflows |
| Move visible titles to Trash | Tools | existing confirmation and removal owner |
| Restore/permanently delete record | Trash | existing database owners with S6 confirmation |
| Export library / edit or save filters | Tools | existing `MainVM` commands |
| Toggle first filter at startup | Tools | existing live preference owner |
| Guided setup review | Settings; preserved Tour intent | `MainVM.StartWalkthroughAsync` routes by active shell mode |

## Onboarding and hosting

`OnboardingViewModel` owns a five-step local draft: profile, accounts, local-file location, scan, and first Current Flight. It delegates real work and does not fabricate accounts, books, paths, queue items, free-space values, or scan percentages. Capture projection can render an active scan stage but cannot start a scan or mutate Flight.

Automatic first run requires an explicit profile choice before Continue. Manual re-entry seeds the saved profile and can close without changes. Completing a contemporary choice persists the profile, first-launch marker, and shell activation through one settings-file transaction; `UseContemporaryShell` is published last. Choosing the current Libation interface leaves the contemporary shell disabled.

Step 4 shows owner scan text and indeterminate activity instead of a fabricated percentage. The user may continue while an owner scan runs. Step 5 offers two explicit outcomes:

- finish without changing Flight; or
- request up to three newest, present, non-deleted, non-parent titles from `MainWindow`, add them through the shell-owned `FlightService`, and open Library without starting processing.

Library is activated before the requested Flight projection is dispatched. This ordering prevents a retained Details-grid selection event from replacing the onboarding gesture. If no title is eligible, Library still opens and the shell reports that outcome; if all candidates are already present, it reports that distinct outcome.

## Capture contract and evidence boundary

`secondary.json` binds 24 route frames: six destinations, both profiles, and Wide 1456 x 1060 plus Compact 960 x 720. Five additional inert frames cover onboarding steps 1 through 5. The temporary capture host sizes both its window and re-parented content to the requested extent. On macOS, direct-window discovery includes a valid isolated layer-zero window even when another Libation instance places it on a different Space.

The current S6 evidence is recorded outside the source repository under `runtime-audit-2026-09-03/S6/`. The approved focused contract gate passes 8/8; the unchanged full Release solution gate passes 1,800 total with 1,777 passed, 23 expected platform/secret-store skips, and 0 failed. Local compilation, automated tests, exact-size captures, accessibility-tree inspection, and scoped interactions do not prove VoiceOver or other assistive technology, keyboard-only traversal, live scan/download/process/destructive outcomes, 200% logical scaling, runtime High Contrast/reduced motion, Windows/Linux, installed packaging, notarization, distribution, publication, rollout, or release.
