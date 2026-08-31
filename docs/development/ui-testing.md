# Contemporary UI evidence guide

This guide separates source/build evidence from interaction, visual, performance,
package, and supported-platform proof. Passing one tier never implies another.

## Current repository runner

Libation's existing test projects use MSTest on Microsoft.Testing.Platform. The
canonical repository guidance is `docs/development/testing.md`; targeted runs
use `dotnet test --project <project.csproj>`. No Avalonia headless project or
contemporary UI fixture is present at this implementation baseline.

## Evidence layers

| Layer | Proves | Does not prove |
|---|---|---|
| Release build | C# and compiled XAML are accepted for the selected target/configuration | startup, focus, rendering, commands, other platforms, packages |
| unit/headless | deterministic resource, persistence, Flight, cache, route, and control contracts | native menus, installed assets, OS rendering |
| isolated app interaction | startup, rollback, primary keyboard flows, dialogs, active processing, focus restoration | other operating systems or packages |
| visual captures | a named profile/state/viewport/scale matches the approved hierarchy | command correctness or performance |
| fixture measurements | latency, realization, and decoded-memory behavior for a named machine/data set | unsupported machines or arbitrary libraries |
| packaged platform run | installer/launcher icon, native menus, file pickers, drag/drop, and OS rendering | another OS, architecture, or desktop environment |

## Required deterministic contracts

When test creation and execution are authorized, cover only contract-bearing
behavior that cannot be established by the Release build:

- persisted enum repair, default-disabled flag, and current-interface rollback;
- atomic profile resource validation and preview isolation;
- route persistence and responsive state transitions at the plan-defined shell
  breakpoints;
- stable-ID Flight selection, hidden selections, remove/clear undo, warning
  confirmation, and blocking preflight;
- Details/Gallery selection synchronization and existing context-command parity;
- cover cancellation, lease disposal, eviction, and viewport-derived byte bound;
- one queue source, stable processing wrappers, aggregate progress, and failure
  wording.

Do not add a test solely to increase a count. Each test must name the plan claim
that would otherwise remain unproven and the distinct failure it detects.

## Visual and accessibility record

Every admitted screenshot must record commit, platform, architecture, desktop
environment where relevant, window size, scale, profile, density, decoration,
motion preference, state, and source reference. Store generated evidence outside
shipping assets. Required states and captures are defined in the agent pack's
`07-execution/VISUAL-REVIEW-CHECKLIST.md`.

Keyboard evidence must include search, primary navigation, Library selection,
view switching, book details, Current Flight remove/undo/process, Decanter
navigation/cancel, advanced menus, dialogs, and current-interface rollback. Screen-reader
records must include names, selected/expanded/busy states, progress, live
announcements, and a non-color status reading.

## Performance and privacy

Performance claims require a deterministic generated library, a named reference
machine, exact startup/filter/scroll markers, and before/after captures. Do not
use real account data or copyrighted covers in screenshots or fixtures. Logs and
screenshots must be reviewed for account identifiers, paths, credentials,
tokens, and personally identifying metadata before admission.

## Current evidence boundary

The contemporary source has Release-build evidence on macOS. Three authorized,
project-scoped filters passed 23 selected settings, persistence, Flight-ID JSON,
and diagnostic-scrubbing cases. No broader suite, isolated runtime interaction,
visual baseline, accessibility tool, large-library measurement, platform
package, or Windows/Linux run has been executed in this workstream. Those tiers
require separate command/environment authority and remain unverified until
reproduced.
