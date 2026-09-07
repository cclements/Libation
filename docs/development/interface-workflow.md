# Interface development workflow

Applies to user-visible Libation Avalonia changes: routes, dialogs, Settings,
menus, shared controls, native integration and the current-interface escape
hatch. Use existing product/command owners; preserve the separate Windows
Classic application. Contemporary Cellar remains opt-in and default-off.

This guide adds UI-specific requirements to the common
[delivery process](delivery-process.md). The integrated workspace roadmap owns
cross-stream priorities; the interface progress ledger owns detailed IC packet
and UI finding states. An explicit UI task does not start an audio packet.

## Current program and document precedence

The September 7, 2026 interface program uses IC-0 through IC-9. In the companion
records checkout, [DEVELOPMENT.md](../../../DEVELOPMENT.md) links the active
implementation plan, progress ledger, surface/state ledger and audit. The
[nested evidence ledger](../design/contemporary-cellar-status.md) retains
earlier S0–S9 receipts, not current whole-app completion.

Those parent links assume the local records/product layout. In a standalone
product checkout, this workflow and [UI evidence guide](ui-testing.md) remain
usable; obtain the active target/packet records before implementing a design
whose target is missing. Do not substitute an old baseline for an absent target.

Current user instructions and existing authorization govern the work. The IC
plan governs current sequence and acceptance; older program/spec/handoff text
is historical for execution order, scope grants and test budgets. Architecture,
command, data and compatibility invariants continue unless deliberately changed.

## Before implementation

1. Reconcile live instructions, Git and the active packet. Record owned paths and
   preserve concurrent work. Choose one user-visible outcome and the failure it
   corrects, rather than a collection of unrelated screen tweaks.
2. Identify affected finding/surface IDs, target frames or family recipes,
   relevant states, command owners and shared consumers. A shared control change
   includes representative containing screens, dialogs and popups.
3. Define the smallest meaningful checks and the platform/environment dependency.
   Include body height, keyboard/focus, content growth and error/cancel paths when
   relevant. Missing equipment is an explicit coverage dependency.
4. Produce concrete alternatives before requesting a consequential product
   decision. Once a target/recipe is admitted, routine implementation and its
   proportionate checks continue under the active request.

A routine correction within existing accepted behavior need not wait for a
whole-app redesign. Record its scope and update the affected target/coverage if
necessary. Do not quietly make a new layout the approved baseline after coding it.

## During implementation

- Reuse semantic tokens and shared state templates; keep focus, selection,
  disabled, error and loading distinct. Avoid per-screen color repairs that
  leave the containing control or popup inconsistent.
- Protect useful body space in both dimensions. Use client DIPs and actual fit;
  check Wide 1448 × 1086, Compact 960 × 720, Narrow 720 × 560 and short/wide
  1448 × 720 where the changed layout participates. OS DPI, text enlargement
  and harness transforms are separate checks.
- Preserve library/query/Flight/queue/settings state through resize, theme
  changes, dialogs and rollback. Labels still invoke the established owner.
- Inspect both contemporary profiles and affected High Contrast states. Check
  long/missing data, enabled/focused controls and the relevant unhappy path.
- Exercise keyboard and pointer behavior when changing interaction. Native
  surfaces receive a named platform protocol; an injected screenshot is not
  proof of their behavior.
- Probe platform-sensitive shared patterns early: shell/windowing in IC-1/2;
  controls, dialogs and automation in IC-3. Record available-host results and
  unavailable cells before propagating those patterns. Final exhaustive
  supported-target evidence belongs to IC-9.

## Verification matched to risk

| Change | Required local verification |
|---|---|
| Documentation only | Content, links, source references, coverage and owned diff; no application build or test |
| Layout, tokens, copy or assets | Affected project compilation plus actual relevant rendered states/fit; token contrast or asset checks when warranted |
| Interaction, state or lifecycle | Above plus focused existing/new regressions for the distinct failure and the affected pointer/keyboard journey |
| Shared controls, shell or modal infrastructure | Above plus representative route/form/popup consumers, profile transitions, opt-out and early platform probes |
| Dependency, native or packaging behavior | Appropriate repository tests and exact artifact/native integration checks; keep audio/package/installed evidence distinct |

Use [testing.md](testing.md) for runner/fixture commands and
[ui-testing.md](ui-testing.md) for UI evidence. Tests must detect an independent
failure; do not write tests that merely repeat the implementation or add tests
for a low-impact reversible copy edit. Run broader relevant checks once for the
integration candidate; repeat only for changed scope, a failure or an unresolved
concern. A full application suite is not a documentation gate.

## Review and closure

Review the final result, not just the changed XAML. Compare actual composition,
hierarchy, imagery, typography, control states and task behavior with the
target. A frame exists only as evidence after its content/state/extent has been
inspected. Changed baselines require the same design justification as changed UI.

Use a proportionate reviewer pass. Rank real defects with source/screenshot
references; fix them or explicitly retain them as open. A separate human or
agent review is useful for shared/high-risk work when available and authorized,
but is not an automatic delegation requirement for every change.

Before marking a packet `verified-in-scope`, record:

- its outcome, source/target/fixture identities and owned diff;
- actual checks/results, accepted images and interaction evidence;
- affected coverage cells and remaining platform/AT limitations;
- reopened/closed findings and the next dependency-ready action.

Update the single progress ledger and compact handoff in the same closeout.
The [PR template](../../.github/PULL_REQUEST_TEMPLATE.md) summarizes the same
evidence for a reviewer. Source, build, render, interaction, assistive technology,
installed artifact and release remain separate claims. Final completion requires
the full surface and platform matrices, not the sum of isolated local passes.

New user-visible work adds its surface/states to the inventory before closure.
If it uses an existing recipe, extend that recipe's coverage. Shared owner/API
changes reopen dependent consumer evidence, including audio/output journeys
where relevant. This keeps the completion contract active after IC-9 as well.
