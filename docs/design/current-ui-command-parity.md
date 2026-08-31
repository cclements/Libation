# Current UI command parity

Measured UI intake baseline: `094e207c0b245f36592ce31000f693674b886057`
(Libation 14.0.0). Delivery baseline:
`3e7191adc7f41f1dec252b95e505b3f318be3b34`; its intervening upstream changes
do not touch the UI command surfaces inventoried here.

This is the preservation contract for the contemporary shell. “Integrated
destination / seam” names presentation wiring now present in the source fan-in,
not a new command implementation. The current owner remains authoritative
through the shared adapters. These mappings are source-level evidence only:
runtime invocation, gesture handling, focus restoration, dialog ownership, and
supported-platform native-menu behavior remain unverified.

## Top-level commands

| Current surface | Command | Current owner | Availability / gesture | Integrated destination / seam |
|---|---|---|---|---|
| Import | Auto Scan Library | `MainVM.ToggleAutoScan` | Accounts only; check state | Accounts through the shared adapter; shell/native menu retained |
| Import | Add Account | `MainVM.AddAccountsAsync` | Shown when no accounts | Accounts through the shared adapter; shell menu retained |
| Import | Scan Library | `MainVM.ScanAccountAsync` | One account; menu-modifier+S on macOS native menu | Exact one-account owner retained in shell/native menus; Accounts exposes the established aggregate scan |
| Import | Scan Library of All Accounts | `MainVM.ScanAllAccountsAsync` | Multiple accounts; menu-modifier+S | Accounts, onboarding, and empty Downloads through the shared adapter; shell/native menus retained |
| Import | Scan Library of Some Accounts | `MainVM.ScanSomeAccountsAsync` | Multiple accounts; menu-modifier+Shift+S | Accounts through the shared adapter; shell/native menus retained |
| Import | Remove Library Books | `MainVM.RemoveBooksAsync` | One account; menu-modifier+R | Exact one-account owner retained in shell/native menus; Accounts presents the established missing-title review |
| Import | Remove Books from All Accounts | `MainVM.RemoveBooksAllAsync` | Multiple accounts; menu-modifier+R | Accounts missing-title review; shell/native menus retained |
| Import | Remove Books from Some Accounts | `MainVM.RemoveBooksSomeAsync` | Multiple accounts; menu-modifier+Shift+R | Accounts missing-title review; shell/native menus retained |
| Import | Locate Audiobooks | `MainVM.LocateAudiobooksAsync` | Always listed | Downloads, Tools, Overview empty/add states, and onboarding through the shared adapter; shell menu retained |
| Liberate | Download/backup all books | `MainVM.BackupAllBooks` | Label is state-derived; macOS `Option+Cmd+B` | Downloads through the shared adapter; shell/native menus retained |
| Liberate | Download/backup all PDFs | `MainVM.BackupAllPdfs` | Label is state-derived; macOS `Option+Cmd+P` | Downloads through the shared adapter; shell/native menus retained |
| Liberate | Convert all M4B to MP3 | `MainVM.ConvertAllToMp3Async` | Explicit long-running warning | Downloads through the shared adapter; shell/native menu retained |
| Liberate | Liberate visible books | `MainVM.LiberateVisible` | Only when visible books need work; macOS `Option+Cmd+V` in native Visible Books | Tools and preserved Library/menu action; Current Flight uses its separate shared preflight path |
| Export | Export Library | `MainVM.ExportLibraryAsync` | In-window `Ctrl+S`; macOS native `Option+Cmd+X` | Tools through the shared adapter; shell/native menus retained |
| Quick Filters | Start with first filter as default | `MainVM.ToggleFirstFilterIsDefault` | Check state | Tools and preserved quick-filter menu |
| Quick Filters | Edit quick filters | `MainVM.EditQuickFiltersAsync` | macOS native `Option+Cmd+Q` | Tools and Library quick-filter menu through the shared adapter; native menu retained |
| Quick Filters | Apply named filter | Dynamic commands in `MainVM.Filters` | macOS `Cmd+1…0`; other platforms F1…F12 | Library quick-filter collection plus preserved menus and gestures |
| Visible Books | Liberate visible | `MainVM.LiberateVisible` | Enabled from current projection | Tools and preserved Library/menu action; Current Flight remains a distinct selected batch |
| Visible Books | Replace Tags | `MainVM.ReplaceTagsAsync` | Current visible projection | Tools through the shared adapter; Visible Books menu retained |
| Visible Books | Set book Downloaded manually | `MainVM.SetBookDownloadedAsync` | Current visible projection | Tools through the shared adapter; Visible Books menu retained |
| Visible Books | Set PDF Downloaded manually | `MainVM.SetPdfDownloadedAsync` | Current visible projection | Tools through the shared adapter; Visible Books menu retained |
| Visible Books | Set Downloaded automatically | `MainVM.SetDownloadedAutoAsync` | Current visible projection | Tools through the shared adapter; Visible Books menu retained |
| Visible Books | Remove from library | `MainVM.RemoveVisibleAsync` | Destructive confirmation | Tools through the shared adapter with the established confirmation; Visible Books menu retained |
| Settings | Accounts | `MainVM.ShowAccountsAsync` | `Ctrl+Shift+A`; macOS `Cmd+.` | Accounts and Settings through the shared adapter; shell/native menus retained |
| Settings | Settings | `MainVM.ShowSettingsAsync` | `Ctrl+P`; macOS `Cmd+,` | Settings and onboarding through the shared adapter; shell/native menus retained |
| Settings | Trash Bin | `MainVM.ShowTrashBinAsync` | Label includes count; macOS `Option+Cmd+T` | Trash and Library recovery through the shared adapter; shell/native menus retained |
| Settings | Launch Hangover | `MainVM.LaunchHangover` | External companion application | Tools; shell/native menu retained |
| Settings | Scan for Better Quality Audiobooks | `MainVM.ShowFindBetterQualityBooksAsync` | In-window and native Settings menus in source | Tools through the shared command adapter; native Settings menu retained |
| Settings | Guided Tour | `MainVM.StartWalkthroughAsync` | Always listed | Contemporary onboarding when the shell is active; current tour when it is off |
| Settings | About | `MainVM.ShowAboutAsync` | Always listed | About route, Settings, and Tools; shell/native menu retained |

## Shell-level controls

| Current control | Current owner | Preservation rule |
|---|---|---|
| Filter syntax help | `MainVM.FilterHelpBtn` | Keep reachable beside search and through keyboard help. |
| Add current search to quick filters | `MainVM.AddQuickFilterBtn` | Keep the existing validation and editor flow. |
| Apply search / press Enter | `MainVM.FilterBtn` plus `MainWindow` key handling | One search/filter source; do not add an independent shell filter. |
| Clear search | `MainWindow.ClearFilterButton_Click` | Clears the authoritative named-filter text. |
| Enter/exit account-book removal mode | `MainVM.RemoveBooksBtn` / `DoneRemovingBtn` | Preserve mode state and destructive wording. |
| Show/hide queue pane | `MainVM.ToggleQueueHideBtn` | Decanter show/focus uses the existing queue; no second queue is created. |
| Empty-library actions | `AddAccountsAsync`, `GettingStartedScanAsync`, `StartWalkthroughAsync` | Preserve all current empty, no-match, and trash-recovery states. |

## Details-grid commands and behavior

`ProductsDisplay` remains the Details implementation. The contemporary shell must
preserve sortable, resizable, reorderable, hideable columns; persisted column
order/width/visibility; multi-row selection; copy; context menus; series
expansion; and the following row actions:

| Action | Current owner | Integrated seam |
|---|---|---|
| Copy cell / row contents | `ProductsDisplay` code-behind | Existing Details grid; Gallery copies selected rows using visible Details columns |
| Liberate all episodes in a series | `ProductsDisplay` event to `MainWindow` | `LibraryView` forwards the existing event seam to `MainWindow` |
| Set book/PDF Downloaded or Not Downloaded | shared `GridContextMenu` | One shared Details/Gallery context-command builder |
| Locate a local file | `ProductsDisplay` plus storage provider | One shared Details/Gallery context-command builder and existing storage owner |
| Remove from library | shared `GridContextMenu` | One shared Details/Gallery context-command builder with existing destructive behavior |
| Download selected / download as chapters | `ProductsDisplay` event to current processing path | Shared Details/Gallery context commands retain the current processing path; Flight has its own batch preflight |
| Convert selected to MP3 / force re-download | `ProductsDisplay` event to current processing path | Shared Details/Gallery context commands retain the current processing path |
| Remove eligible Plus titles from Audible | shared `GridContextMenu` | One shared Details/Gallery context-command builder |
| Edit folder/file/chapter templates | `ProductsDisplay` dialogs | Shared Details/Gallery context commands open the existing dialogs |
| View bookmarks/clips / view series | `ProductsDisplay` dialogs | Shared Details/Gallery context commands and book-details actions open the existing dialogs |
| Edit tags / click cover / open release link | `ProductsDisplay` code-behind | Existing Details behavior plus Gallery/shared book-details seams |

The DataGrid's selected rows are not the Flight. The contemporary shell now owns
one stable-ID `FlightService`; Details and Gallery adapt their selection into
that service, and every route/profile Flight surface observes the same instance.

## Queue commands and behavior

`ProcessQueueViewModel` in `LibationUiBase` is authoritative for the concurrent
queue. Decanter presents this same instance and must preserve:

| Action/state | Current owner | Preservation rule |
|---|---|---|
| Per-job cancel/retry/status/progress | `ProcessBookViewModel`, `ProcessBookControl`, and `MainVM.QueueBooksAsync` | Use existing job identity and state transitions; contemporary Retry is shown only for failed download/decrypt work whose book and effective configuration remain available. |
| Cancel all | `ProcessQueueControl.CancelAllBtn_Click` | Route to the shared VM/queue only. |
| Clear finished | `ProcessQueueControl.ClearFinishedBtn_Click` | Preserve active jobs. |
| Concurrent-download count | `ProcessQueueViewModel.MaxConcurrentDownloads` | Keep validation, CPU hint, and persistence. |
| Download speed limit | `ProcessQueueViewModel.SpeedLimit` | Keep the existing units and persistence. |
| Auto-scroll | `ProcessQueueViewModel.AutoScrollQueue` | Retain as a presentation preference. |
| Aggregate counts and progress | `ProcessQueueViewModel` | Decanter summary projects these values. |
| Open / copy / clear queue log | `ProcessingViewModel` and `ProcessQueueControl` | Failed rows select the Processing log workspace and its inner Queue Log tab; the existing copy and clear actions remain available. |

Only the visible legacy queue control subscribes to `ProcessBookControl` static
events; `ProcessQueueControl` now attaches and detaches those handlers with its
visual-tree lifetime. Decanter still projects the single shared queue rather than
creating another execution owner.

## Reconciled baseline discrepancy

The baseline macOS native Settings menu omitted “Scan for Better Quality
Audiobooks,” although the in-window menu exposed it. `MainWindow.axaml` now
retains that command in the native Settings menu, and the integrated
contemporary Tools destination reaches the same existing command owner.
Runtime native-menu invocation, route-to-command execution, gesture handling,
focus restoration, and dialog behavior remain unverified parts of the
supported-platform interaction matrix.
