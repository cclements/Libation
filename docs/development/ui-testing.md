# Contemporary UI evidence guide

This guide separates source/build evidence from interaction, visual, performance,
package, and supported-platform proof. Passing one tier never implies another.

Follow the [interface workflow](interface-workflow.md) for scoping and closure.
The active IC plan and surface ledger in the companion records checkout define
required cases; historical screenshot counts do not close their open findings.

## Current repository runner

Libation's existing test projects use MSTest on Microsoft.Testing.Platform. The
canonical repository guidance is [testing.md](testing.md); targeted runs
use `dotnet test --project <project.csproj>`. The current tree contains
`Source/_Tests/LibationAvalonia.Tests`, including shell/state, Flight,
Processing, onboarding, capture-plan and headless-render contracts. Existing
scripts generate deterministic fixtures and capture the app. Read the current
source and select relevant contracts; do not add another harness because an
older baseline document says these tools are absent.

From the product repository, implementation checks may use:

```bash
dotnet build Source/LibationAvalonia/LibationAvalonia.csproj -c Release --disable-build-servers -m:1
dotnet test --project Source/_Tests/LibationAvalonia.Tests/LibationAvalonia.Tests.csproj
bash Scripts/capture-ui.sh /path/to/isolated-profile /path/to/plan.json /path/to/evidence --no-build
```

These are command forms, not instructions to run all three for every task.
Check the installed SDK and runner's filter syntax; record selected test
identities/counts, not only exit status. `capture-ui.sh` builds to the
companion `.demo/capture-app` unless `--no-build` is used. A normal project
build does not refresh that capture apphost. Identify the exact binary before
using `--no-build`; omit that flag only when a capture build is part of the
current implementation checks. Bound waits and clean up only fixture-owned
processes. Documentation-only work runs none of these app commands.

## Evidence layers

| Layer | Proves | Does not prove |
|---|---|---|
| Release build | C# and compiled XAML are accepted for the selected target/configuration | startup, focus, rendering, commands, other platforms, packages |
| unit/headless | deterministic resource, persistence, Flight, cache, route, and control contracts | native menus, installed assets, OS rendering |
| isolated app interaction | the named startup, rollback, keyboard, dialog, processing or focus paths actually exercised | unexercised paths, other operating systems or packages |
| visual captures | a named profile/state/viewport/scale matches the approved hierarchy | command correctness or performance |
| manual assistive technology | the named screen-reader/keyboard journey on the recorded OS and app | another AT, platform, or unexercised route/dialog |
| fixture measurements | latency, realization, and decoded-memory behavior for a named machine/data set | unsupported machines or arbitrary libraries |
| packaged platform run | installer/launcher icon, native menus, file pickers, drag/drop, and OS rendering | another OS, architecture, or desktop environment |

## Required deterministic contracts

During authorized implementation, use proportionate existing/new contracts for
distinct failures that cannot be established by the Release build:

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

The current IC risks include About readiness termination, malformed capture
extent rejection, short-height body fit, discoverable navigation, semantic
selected/error colors and modal cleanup/focus. Target these failures where the
packet changes them. A broad relevant suite belongs to candidate integration;
repeat it only for changed scope, failures or unresolved concerns.

## Visual and accessibility record

Every admitted screenshot must record commit, platform, architecture, desktop
environment where relevant, window size, scale, profile, density, decoration,
motion preference, state, and source reference. Also record binary/fixture
identity, actual client extent, profile/draft state and the target/recipe. Store
generated evidence outside shipping assets. Use the current surface/state
ledger for required coverage; the original pack's visual checklist remains a
comparison aid, not a substitute for dialog/state/platform coverage.

Layout coverage uses client DIPs: W 1448 × 1086, C 960 × 720, N 720 × 560 and
short/wide H 1448 × 720 when relevant. Check width boundaries and available
body height for shared geometry changes. A normal record's identity/action and
every destination must remain reachable; a scrollable sliver or zero-height
body fails. Actual OS DPI and 200% text enlargement are separate from the
harness's `logicalScale` transform. Dialogs fit their natural size and current
work area, rather than being stretched to fill a reference screenshot.

Inspect saved pixels before accepting a frame. Reject wrong state/profile,
blank content or malformed extent; preserve the rejection and replacement
identity. Compare matching fixture/state/viewport against the admitted target.
Baseline updates are reviewed as design changes. File production and a zero
exit code do not establish visual acceptance.

Keyboard evidence must include search, primary navigation, Library selection,
view switching, book details, Current Flight remove/undo/process, Decanter
navigation/cancel, advanced menus, dialogs, and current-interface rollback. Screen-reader
records must include names, selected/expanded/busy states, progress, live
announcements, and a non-color status reading.

Test keyboard and changed control semantics within their implementation packet.
Probe platform-sensitive shell/modal patterns early on available hosts; record
unavailable environments. IC-9 owns the final installed OS/architecture and AT
matrix. Screenshots and automation-tree peers cannot substitute for those runs.

## Performance and privacy

Performance claims require a deterministic generated library, a named reference
machine, exact startup/filter/scroll markers, and before/after captures. Do not
use real account data or private library content in shared fixtures. Use synthetic,
public-domain or appropriately licensed cover samples. Logs and
screenshots must be reviewed for account identifiers, paths, credentials,
tokens, and personally identifying metadata before admission.

## Current tooling and evidence limits

At the September 7 source baseline, `CaptureSurface` supports Route,
ComponentGallery and Onboarding. It has no first-class Dialog/Window/Message
capture entry. About's accepted route cannot satisfy its rendered-readiness
predicate. The audit also rejected malformed narrow captures and qualified
mixed profile/draft state. IC-1 owns those reliability and coverage corrections.
Until fixed, use a specific attended protocol for unavailable surfaces; retain
blocked coverage rather than treating a timeout as an inspected screen.

Use copied profiles and inert fixtures selected through `LIBATION_FILES_DIR`.
Do not point fixture seeding/cleanup at a normal profile. Prevent automatic
scan/update/download and real secret-store access according to the fixture
instructions. Native pickers, authentication and real operations need their
own attended test scope; visual fixtures do not prove them.

On this macOS host, the capture driver's Swift window helper may need writable
`CLANG_MODULE_CACHE_PATH` and `SWIFT_MODULECACHE_PATH` under `/private/tmp`.
That helper compilation is distinct from building the application. The driver
has a full-run timeout; IC-1 must also make individual readiness waits finite.

The [status ledger](../design/contemporary-cellar-status.md) owns historical
source/automation/headless/package receipts and links to current progress.
Installed-platform, whole-app manual accessibility and final visual completion
remain open. This guide's September 7 reconciliation is source/document
inspection, not a new runtime or test result.
