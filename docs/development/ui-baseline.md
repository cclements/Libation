# Avalonia UI baseline

Captured 2026-08-30 from
`094e207c0b245f36592ce31000f693674b886057` (`origin/master`, Libation 14.0.0).
Measurements in this file are admitted only when the command and input are
reproducible. Missing fixtures are recorded as gaps rather than replaced with
estimated numbers.

## Toolchain and solution

- SDK policy: `global.json` requests .NET SDK 10.0.101 with feature-band roll
  forward; this workstation resolves `/Users/chris/.dotnet/dotnet` 10.0.302.
- Product target frameworks: `net10.0`; Windows UI/loaders use
  `net10.0-windows7.0`.
- Avalonia application: `Source/LibationAvalonia/LibationAvalonia.csproj`.
- Direct application dependency: `LibationUiBase`; domain and persistence layers
  remain below it.
- Avalonia packages: Desktop, Fluent, and ColorPicker 12.0.2; DataGrid and WebView
  12.0.0. Debug diagnostics remains 11.3.14.
- The solution contains core/domain/application libraries, Avalonia and WinForms
  applications, CLI, OS loaders, Hangover applications, demos, and nine test
  projects plus their shared assertion helper.
- CI builds/tests/publishes on Windows, macOS, and Linux. A local macOS result is
  not cross-platform evidence.

## Reproducible baseline commands

Run from the repository root:

```sh
/Users/chris/.dotnet/dotnet --version
/Users/chris/.dotnet/dotnet restore Source/LibationAvalonia/LibationAvalonia.csproj
/Users/chris/.dotnet/dotnet build Source/LibationAvalonia/LibationAvalonia.csproj --configuration Release --no-restore
```

The first no-restore build correctly reported missing restored package
`Avalonia 12.0.2`. The explicit restore then succeeded for 14 project graphs.
The first post-restore Release build ran for `00:10:01.87`, then exited 1 while
reporting `0 Warning(s)` and `0 Error(s)`. A serialized retry exposed the cause:
Avalonia BuildServices attempted to write
`~/Library/Application Support/AvaloniaUI/BuildServices/buildtasks.log`, which
the workspace sandbox denied. This was a host/sandbox failure, not a compiler
diagnostic.

With that exact Avalonia log write allowed, the serialized Release build of the
current baseline plus profile substrate succeeds with zero warnings and zero
errors (latest measured run `00:00:12.88`):

```sh
/Users/chris/.dotnet/dotnet build Source/LibationAvalonia/LibationAvalonia.csproj --configuration Release --no-restore --disable-build-servers -m:1 -v:minimal
```

No test command is part of this baseline. Tests require separate, current
approval identifying the exact command and evidence gap.

## Current ownership and extension points

| Concern | Current owner | Measured/source baseline | Compatible extension |
|---|---|---|---|
| Window and startup | `App.ShowMainWindow`, `MainWindow` | One native window; startup applies global theme handlers before constructing `MainVM` | Keep `MainWindow` transitional and choose its hosted view behind the disabled flag. |
| Menus/navigation | `MainWindow.axaml`, `MainVM` partials | Native and in-window menus; no route model | Add one route service and command adapters without copying commands. |
| Library | `ProductsDisplayViewModel`, `ProductsDisplay` | One source list and `DataGridCollectionView`; binding after source construction saves about 500 ms at an observed ~4,500-book library according to the existing source comment | Retain DataGrid as Details; expose a read-only projection/selection adapter for Gallery. |
| Search/filter | `MainVM.Filters`, `ProductsDisplayViewModel` | Search engine filters the authoritative DataGrid projection; dynamic quick-filter gestures | Reuse one filter state in all surfaces. |
| Selection | `ProductsDisplay` DataGrid | Multi-selection is view-owned; no reusable shell selection service | Introduce one stable-ID Flight service before cross-route selection. |
| Queue | shared `ProcessQueueViewModel`, `ProcessQueueControl` | Concurrent, multi-active queue; list uses `VirtualizingStackPanel`; queue/log split | Decanter projects the same VM. |
| Theme | `App`, `ChardonnayTheme`, `ChardonnayThemePersister` | System/Light/Dark plus `ChardonnayTheme.json`; deferred Fluent reload avoids popup crashes | Add profile dictionaries and manager; retain Chardonnay adapter. |
| Theme preview | `ThemePreviewControl` | Live controls, queue states, and a mock `ProductsDisplay` already exist | Extend it with scoped profile resources. |
| Window state | `FormSaveExtension` | Size/location and queue-open state persist | Keep keys/semantics through shell migration. |

## Runtime measurements

| Measurement requested by the plan | Current result | Reproduction / gap |
|---|---|---|
| Cold start | Not admitted | App startup reads the user's Libation configuration/library and may initiate account/update work. No isolated launch profile is currently documented. |
| First useful render | Not admitted | Requires an instrumented, isolated launch definition and a precise “useful” marker. |
| Idle memory | Not admitted | Requires the same isolated launch definition and a steady-state marker. |
| Library load | Only the existing source comment: roughly 500 ms avoided by delayed binding at roughly 4,500 books | Not a fresh measurement. Repository has no deterministic 1k/10k/50k Avalonia library fixture. |
| Search latency | Not admitted | No deterministic large fixture or benchmark harness exists. |
| Queue update behavior | Source-verified, not timed | Shared queue now supports multiple active jobs, concurrency, speed limits, auto-scroll, counts, progress, and logs. A real run changes external/library state. |
| Window resize | Source-verified, not visually timed | Current content uses fixed/right `SplitView` queue geometry; no responsive shell or breakpoint service exists. |

An authorized benchmark fixture must isolate configuration, avoid accounts and
network, generate stable synthetic book identities, define the reference machine,
and state exact measurement markers. Until then, the plan's 10k/50k and latency
numbers are targets without current repository evidence, not passed gates.

## Current behavioral constraints

- `MainVM` owns exactly one library VM and one queue VM and retains the concrete
  window for dialogs, keybindings, menus, and storage-provider calls.
- DataGrid column width, order, and visibility are user-persisted behavior.
- Quick filters own `Cmd+1…0` on macOS and F1…F12 elsewhere.
- The current theme validator accepts only Light and Dark effective variants.
- Queue rows are virtualized, but duplicate live queue controls are unsafe because
  static process-book events are subscribed without a teardown contract.
- Current empty, no-match, and trash-recovery states are implemented in
  `MainWindow`; the new shell must carry each state forward.

## Baseline discrepancies from the plan

1. There is no `AppShellView`, route service, `ExperienceManager`, persisted
   experience enum, Flight service, Avalonia UI test project, or Headless package.
2. The “Classic” profile name collides with the shipped WinForms artifact.
3. Planned numeric route shortcuts collide with macOS quick filters.
4. High contrast cannot be routed through the existing Chardonnay validator.
5. A useful live theme preview already exists and should be extended.
6. Selection is not yet a reusable service; the library source is private.
7. The queue already has parallel-download semantics newer than the plan and must
   remain authoritative.
8. Native and in-window command sets are not fully equivalent.
9. The repository has no reproducible large-library performance fixtures.
