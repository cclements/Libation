# Contemporary secondary destinations

Prompt 10 is implemented as presentation over the existing `MainVM`,
`ProductsDisplayViewModel`, `ProcessQueueViewModel`, dialogs, configuration, and
command owners. The feature views do not open their own account persister, create a
second downloader, reproduce settings validation, or mutate database rows directly.

The current Libation interface, the contemporary shell, native menus, established
dialogs, and existing keyboard gestures remain available together during the
experimental opt-in rollout.

## Construction and lifetime

`LibationCommandAdapter` is the shared bridge to established command owners.
`AppShellViewModel` constructs one instance of each destination view model and owns
its lifetime:

```csharp
var adapter = new LibationCommandAdapter(main);

var downloads = new DownloadsViewModel(adapter);
var history = new HistoryViewModel(main);
var accounts = new AccountsViewModel(adapter);
var settings = new SettingsViewModel(adapter, configuration);
var tools = new ToolsViewModel(adapter);
var trash = new TrashViewModel(adapter);
```

Each instance must be reused while its route is hidden so an in-flight action,
search, and live aggregate do not restart on every navigation. The shell disposes
all six instances. `SecondaryDestinationViewModel` serializes owner actions through
a nonblocking gate, reports a literal local error, logs the exception, and disposes
only commands it created. It never disposes a `MainVM`-owned command.

## Destination contracts

| Destination | Authoritative data | Production states | Delegated actions |
|---|---|---|---|
| Downloads | `MainVM.LibraryStats`; shared `ProcessQueue.Queue` | counts loading; no catalogued titles with account-aware next action; Download Pending; downloaded Audible file; processed open copy; unavailable/error; queue idle/active | pending books, pending PDFs, locate existing files, convert M4B library to MP3, account management/scan for an empty library, refresh counts |
| History | `LibraryBook.DateAdded`; `UserDefinedItem.LastDownloaded`; current retained `ProcessQueue.LogEntries` | asynchronous loading; searchable results; no match; source/projection error | refresh only; no mutation |
| Accounts | aggregate `MainVM` account, scan, auto-scan, and library counts | no accounts; connected account count; scanning; idle; automatic scan on/off | add/manage, scan all/some, toggle automatic scan, run existing missing-title review for all/some |
| Settings | `Configuration` summaries; stable local category index | all categories; filtered categories; no match; owner-dialog error | established Settings, Accounts, and About dialogs; request contemporary onboarding re-entry |
| Tools | literal static command catalogue; `MainVM` commands | grouped low-risk, preference-changing, scope-sensitive, file-writing, destructive-after-confirmation, external-utility actions | existing library maintenance, file discovery, metadata, quality, export, quick-filter, About/update, and Hangover owners |
| Trash | `MainVM.BooksInTrash` | empty; items present; owner-dialog error | refresh count; open the existing searchable restore/permanent-delete dialog |

All profile-dependent colors, typography, spacing, decoration, and status treatment
come from `Libation.*` resources. Feature views consume the shared `PageHeader`,
`MetricCard`, `StatusBadge`, `AttentionBanner`, `EmptyState`, and
`ThemePreviewCard` components instead of local substitutes.

## Truth and privacy boundaries

### Downloads

The backend currently combines acquisition and processing in some queue operations.
The page explains the states separately but invokes the same combined command. Its
counts are filesystem-aware `LibraryStats` results. “Downloaded Audible file” is a
subset of “Download Pending” until an open copy completes, and the page labels that
overlap explicitly instead of presenting the metrics as disjoint. It does not infer or fabricate
per-title account, marketplace, source quality, expected size, missing-file reason,
retryability, or progress when those facts are not exposed by one stable destination
source. Title-level queue outcomes remain in Processing and title facts remain in
Library.

### History

History is explicitly a limited projection, not an audit log. At most two persisted
timestamp rows are projected for a title: catalogued and last completed download.
Queue messages cover only the current retained processing session. Scans, exports,
metadata edits, removals, restores, retries, account, marketplace, and durable
failure history are absent unless a future domain-owned event store records them.

The library snapshot is captured on the UI thread, projection/sort runs on a worker,
refresh requests coalesce, and the final `ListBox` owns a bounded `*` row with a
`VirtualizingStackPanel`. No ancestor `ScrollViewer` defeats virtualization.

### Accounts

The destination projects counts and coarse operational state only. It never opens
`AudibleApiStorage`, reads account rows, or exposes names, login addresses, locale,
marketplace IDs, cookies, credentials, tokens, or copyable diagnostics. Identities
and marketplaces stay in `AccountsDialog`. There is no reliable persisted
per-account last-successful-scan or authorization-health aggregate, so the page does
not claim either one. Existing scan/sign-in owners surface authorization requests
when required.

Account removal remains inside the established account dialog. That dialog currently
does not provide a consequence-focused confirmation; the aggregate page therefore
does not surface removal itself or imply that a confirmation exists.

### Settings, About, and updates

Search indexes only stable category names, descriptions, and generic search terms;
it never indexes current values, paths, account identities, or secrets. Actual edits
open `SettingsDialog`, which remains responsible for validation, directory creation,
secure token conversion, preview/cancel, and saving. The appearance summary observes
the existing configuration properties and does not write them.

The existing Chardonnay editor remains accessible as an advanced override surface.
Complete profile defaults and legacy palette overrides are not flattened or migrated
by the category index. About/version/release notes/update checking and installation
remain owned by the established About dialog, reachable from Settings, Tools, the
About route, and the preserved menu.

### Tools and Trash

Tool labels are literal and every entry states its consequence and risk. Commands
that say “visible titles” mean every title matching the shared Library filter, not
only rows in the viewport. Destructive actions retain their established preview or
confirmation where one already exists.

The Trash destination is a gateway, not a second deletion implementation. The
established dialog provides search, selection, restore, and permanent record
deletion. Its permanent action currently removes the selected `Book` and
`LibraryBook` database records immediately after activation and does not add another
confirmation. The destination warns about that exact behavior and does not claim
that open audiobook files are deleted from disk.

## Command parity

| Existing command/workflow | Contemporary destination | Owner preserved |
|---|---|---|
| Add account | Accounts; onboarding | `MainVM.AddAccountsAsync` |
| Toggle automatic scan | Accounts | `MainVM.ToggleAutoScan` |
| Scan one/all accounts | Accounts; onboarding; empty Downloads | `MainVM.ScanAllAccountsAsync` through adapter |
| Choose accounts to scan | Accounts | `MainVM.ScanSomeAccountsAsync` |
| Review removed titles for one/all accounts | Accounts | `MainVM.RemoveBooksAllAsync` |
| Choose accounts for removed-title review | Accounts | `MainVM.RemoveBooksSomeAsync` |
| Locate audiobooks | Downloads; Tools; onboarding | `MainVM.LocateAudiobooksAsync` |
| Begin book backups | Downloads | `MainVM.BackupAllBooks` |
| Begin PDF-only backups | Downloads | `MainVM.BackupAllPdfs` |
| Convert all M4B to MP3 | Downloads | `MainVM.ConvertAllToMp3Async` and its confirmation |
| Process visible titles | Tools; preserved Library/menu action | `MainVM.LiberateVisible` |
| Export library | Tools | `MainVM.ExportLibraryAsync` |
| Apply a saved quick filter | Library quick-filter collection and preserved menu/gestures | existing generated quick-filter commands |
| Edit quick filters | Tools; Library quick-filter menu | `MainVM.EditQuickFiltersAsync` |
| Save current filter | Tools; Library action | `MainVM.AddQuickFilterBtn` |
| Toggle first quick filter at startup | Tools; preserved quick-filter menu | `MainVM.ToggleFirstFilterIsDefault` |
| Filter syntax help | Tools; Library action | `MainVM.FilterHelpBtn` |
| Replace visible-title tags | Tools | `MainVM.ReplaceTagsAsync` and established prompts |
| Set visible audiobook status | Tools | `MainVM.SetBookDownloadedAsync` |
| Set visible PDF status | Tools | `MainVM.SetPdfDownloadedAsync` |
| Detect visible download status | Tools | `MainVM.SetDownloadedAutoAsync` |
| Move visible titles to Trash | Tools | `MainVM.RemoveVisibleAsync` and established confirmation |
| Manage accounts | Accounts; Settings | `MainVM.ShowAccountsAsync` |
| Edit settings | Settings; onboarding | `MainVM.ShowSettingsAsync` |
| Restore/permanently delete library records | Trash | `MainVM.ShowTrashBinAsync` and `TrashBinViewModel` |
| Launch Hangover | Tools | `MainVM.LaunchHangover` |
| Scan for better-quality audiobooks | Tools | `MainVM.ShowFindBetterQualityBooksAsync` |
| About/version/update status | Settings; Tools; About route | `MainVM.ShowAboutAsync` |
| Guided Tour / contemporary setup review | current interface uses the established tour; contemporary shell opens repeatable onboarding | `MainVM.StartWalkthroughAsync` routes by the active shell mode |

The current guided tour is coupled to controls in the current Libation interface.
`MainVM.StartWalkthroughAsync` therefore keeps that tour when the feature flag is off
and routes the same menu intent to repeatable profile-aware onboarding while the
contemporary shell is active. The two presentations share an entry point without
pretending their control-level walkthroughs are interchangeable.

## Onboarding and hosting API

`OnboardingViewModel` owns a five-step local draft: profile, accounts, locations,
scan, and first Current Flight. It delegates all real actions and never fabricates an
account, book, queue item, location, free-space value, or output path. Profile cards
use isolated `ExperienceManager.CreatePreviewScope` resources and do not mutate the
active application profile.

The presentation host, not the view model, owns overlay/window policy. `MainWindow`
now implements this sequence:

1. Construct `new OnboardingViewModel(adapter, isManualReentry, configuration)`.
2. For automatic first run, present only when `ShouldOfferAutomatically` is true;
   the former first-launch tour prompt no longer clears `Configuration.FirstLaunch`
   first, so the two flows cannot compete.
3. For re-entry, subscribe to `SettingsViewModel.OnboardingRequested` and construct
   with `isManualReentry: true`.
4. Set a new `OnboardingView` data context to that instance as a first-class main
   window surface. Existing account/settings/location dialogs retain `MainWindow` as
   owner and scan/library work remains nonblocking.
5. Subscribe to `ExitRequested`; on completion or skip, unsubscribe, dispose, and
   reapply the saved shell mode. Window close performs the same cleanup.

Automatic first run starts with a Follow System draft, but that draft is not an
opt-in. Continue is stopped until the user explicitly selects Follow System or
another profile card. Skip changes only `Configuration.FirstLaunch`; it never writes
`UseContemporaryShell` or `ExperienceStyle`. Finish commits the explicit choice:

| Choice shown to the user | Persisted result |
|---|---|
| Follow System | `ExperienceStyle.FollowSystem`, then `UseContemporaryShell = true` |
| Cellar | `ExperienceStyle.Cellar`, then `UseContemporaryShell = true` |
| Tasting Room | `ExperienceStyle.TastingRoom`, then `UseContemporaryShell = true` |
| High Contrast | `ExperienceStyle.HighContrast`, then `UseContemporaryShell = true` |
| Current Libation interface | `UseContemporaryShell = false` |

`UseContemporaryShell` remains default-off. “Current Libation interface” is the user
label; `ExperienceStyle.CurrentAvalonia` remains the internal preview/profile name.
The separately shipped Libation-Classic application is not an onboarding choice.
Manual re-entry seeds the saved profile and may close without changes. Construction,
Back, preview, and search do not write settings.

Contemporary finishes persist the selected experience, the completed first-launch
marker, and shell activation with one atomic settings-file replacement. Configuration
change notifications are published only after that replacement succeeds, with
`UseContemporaryShell` last, so presentation subscribers cannot observe a partially
applied profile.

## Integration gaps and evidence boundary

1. Downloads requires a future domain adapter for per-title marketplace, source
   quality, expected size, retryability, and acquisition progress. Aggregate state
   and established actions are production-safe without those claims.
2. Rich History requires a durable domain event schema. The current page is honest
   and useful but intentionally incomplete.
3. A privacy-safe per-account adapter is required before account cards can show
   nickname, marketplace count, title count, last successful scan, or authorization
   health. Do not derive these by opening the account persister in the view model.
4. Account removal and Trash permanent deletion lack consequence-focused
   confirmations in their established dialogs. This tranche warns or withholds the
   action rather than duplicating domain logic.
5. Release compilation proves the current source/XAML target. Keyboard traversal,
   screen-reader output, runtime profile switching, supported-platform behavior,
   screenshots, automated tests, and interaction remain separate unverified gates.
