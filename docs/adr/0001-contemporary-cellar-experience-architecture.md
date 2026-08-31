# ADR 0001: Contemporary Cellar experience architecture
- Status: Accepted for implementation
- Date: 2026-08-30
- Baseline: `094e207c0b245f36592ce31000f693674b886057` (`origin/master`, Libation 14.0.0)
- Decision owners: Contemporary Cellar program

## Context

Libation's Avalonia application currently has one `MainWindow` and one `MainVM`.
That view model owns the single `ProductsDisplayViewModel` and the single shared
`ProcessQueueViewModel`. Menus, shortcuts, dialogs, storage pickers, library
selection, DataGrid customization, and queue presentation still span XAML,
view-model partials, and code-behind. The existing Chardonnay theme supports
Fluent Light and Dark plus persisted overrides; it is not an experience-profile
system.

The Contemporary Cellar program adds two first-class presentations, Cellar and
Tasting Room, without changing the user's library or processing semantics. The
reference boards are visual direction, not source assets or literal geometry.

## Decision

### One application state graph

The new shell will adapt the existing `MainVM` and its existing library and queue
instances. It must not copy books into a second mutable collection, create a
second process queue, or mount two legacy `ProcessQueueControl` instances at the
same time. `ProductsDisplay` remains the authoritative Details view during the
migration.

The future Flight will have one service keyed by stable book identity. Details,
Gallery, Overview, and both profile-specific Flight surfaces will project that
same service. A view-local selection is never a second Flight.

### One codebase, two compositions

Cellar and Tasting Room share domain services, commands, semantic tokens,
controls, routes, accessibility behavior, and status meanings. Profiles may
change palette, density, decoration, and layout composition. They may not fork
business behavior or introduce profile-specific domain state.

### Separate experience persistence from the legacy theme setting

The existing `Configuration.ThemeVariant` (`System`, `Light`, `Dark`) remains
backward compatible. New persistence lives in `LibationFileManager` using a
UI-agnostic enum/DTO; the Avalonia layer maps it to an effective profile.
Persisted values must not reference Avalonia types.

The initial compatibility defaults are:

- `UseContemporaryShell = false` when absent;
- experience preference `FollowSystem` when absent, but it is inert while the
  contemporary shell is disabled;
- the existing theme and Chardonnay overrides continue to render exactly as
  they do now;
- invalid new values fall back to the current Avalonia presentation and are
  logged without rewriting unrelated settings.

Within the contemporary shell, `FollowSystem` resolves dark appearance to
Cellar and light appearance to Tasting Room. Explicit Cellar and Tasting Room
choices do not change when the operating-system appearance changes.

### Name the fallback unambiguously

The plan's in-app label `Classic` conflicts with the shipped Windows
`Libation-Classic` WinForms artifact. Code and diagnostics will call the existing
Avalonia fallback `CurrentAvalonia`; user-facing copy will say “Current Libation
interface.” The release artifact names remain unchanged.

### Semantic resources first

Feature views consume semantic resources such as surfaces, text, borders,
selection, focus, status, spacing, typography, motion, and elevation. Profile
palettes provide those values. Theme-dependent values use `DynamicResource`;
immutable templates and geometries use `StaticResource`. Raw profile colors are
not allowed in feature views.

`ExperienceManager` will own effective-profile resolution, profile dictionary
application, system-theme observation, scoped preview resources, density,
decoration, and reduced-motion mapping. Existing Chardonnay behavior remains
behind an adapter for the current interface.

High contrast is a complete semantic palette and does not pass through
`ChardonnayTheme`'s Light/Dark-only validation. If a contemporary palette cannot
load, the application falls back to the current interface or the high-contrast
semantic palette and records the literal reason.

### Incremental shell migration

`MainWindow` remains the native window during migration. The contemporary shell
is introduced behind `UseContemporaryShell`, initially disabled, and hosts the
same `MainVM`, `ProductsDisplayViewModel`, and `ProcessQueueViewModel`. Command
adapters will be extracted from existing owners as needed; a flag-day MVVM or
framework rewrite is prohibited.

The first release keeps native chrome and native macOS menus. Existing quick
filter shortcuts retain `Cmd+1…0`; primary navigation must use a non-conflicting
gesture that is selected and documented with the route workstream.

### Rollout and rollback

Every contemporary surface remains reachable only through the disabled flag
until its launch gate is evidenced. Turning the flag off must restore the current
Avalonia presentation without changing library data, queue state, legacy theme
preferences, or Chardonnay overrides. The fallback remains supported through at
least one stable release after a default-rollout decision.

## Consequences

- The compatibility substrate can land without a visible production change.
- Existing command and DataGrid code-behind may be adapted gradually; it is not
  copied into a parallel shell view model.
- Gallery requires a library projection/selection adapter before it can be
  implemented correctly.
- Decanter must present the existing concurrent queue, including multi-active
  jobs, speed limit, concurrency, auto-scroll, and logs.
- Profile preview extends `ThemePreviewControl` with scoped resources rather
  than globally switching the application.
- The current positional native-menu lookup is a migration risk and must be
  replaced by named command adapters before menus are reorganized.
- Cross-platform, performance, visual-regression, and test completion remain
  separate evidence gates; a macOS build cannot satisfy them.

## Corrected plan assumptions

| Plan assumption | Current evidence | Binding correction |
|---|---|---|
| `Ctrl/Cmd+1…8` is available for routes | macOS quick filters already use `Cmd+1…0` in `MainVM.Filters` | Preserve quick-filter gestures; route work chooses a different chord. |
| `Classic` uniquely means the fallback UI | Windows builds already publish a WinForms `Libation-Classic` artifact | Use `CurrentAvalonia` internally and “Current Libation interface” in UI. |
| High contrast can be another Chardonnay variant | `ChardonnayTheme` validates only Light and Dark | Use an independent semantic high-contrast palette. |
| A new preview harness is needed | `ThemePreviewControl` already exercises live controls and mock library/queue state | Extend and isolate the existing preview. |
| Selection is ready to become Flight | DataGrid selection and the private library source are view-owned | Add a stable-ID adapter and one Flight service before new selection surfaces. |
| Queue presentation can be independently rebuilt | `ProcessQueueViewModel` is shared and concurrent; legacy controls use static events | Reuse one VM and avoid simultaneously mounting duplicate legacy controls. |
| Large-library gates have fixtures | The repository has no reproducible 10k/50k Avalonia fixture | Create an authorized deterministic fixture before claiming those gates. |
| Native and in-window menus are already equivalent | macOS native Settings omits “Scan for Better Quality Audiobooks” | Treat parity as an explicit inventory and repair task. |

## Rejected alternatives

- A second application or profile-specific fork: rejects shared behavior and
  multiplies migration risk.
- A flag-day replacement of `MainWindow`/`MainVM`: breaks existing dialog,
  shortcut, selection, and command ownership before adapters exist.
- Shipping the reference PNGs as interface assets: the boards contain mock
  content and rasterized UI; production resources must be code-native or owned
  vector/raster assets with provenance.
- Reinterpreting `Configuration.ThemeVariant` as an experience: would silently
  change existing user settings and couple the persistence layer to Avalonia.
