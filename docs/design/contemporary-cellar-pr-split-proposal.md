# Contemporary Cellar upstream PR split proposal

Status: proposal only. This document does not authorize or open a pull request, contact a maintainer, publish an artifact, or enable the contemporary shell by default.

## Split rules

- Re-cut every PR from the then-current upstream `master`; do not ask maintainers to review the historical merge-heavy development branch.
- Keep `UseContemporaryShell` default-off in every intermediate PR. A partially landed stack must preserve the current Avalonia and Classic applications.
- Preserve one library, one shell-scoped Flight, one existing process queue, and one route service. No PR may introduce a temporary parallel owner.
- Each PR must compile and pass its own affected-project gate. Cross-platform, package, visual, accessibility, and interaction claims remain attached only to evidence that directly proves them.
- Keep generated capture evidence and local demo databases out of source PRs. Commit only reviewed source baselines and durable test/capture machinery.

## Dependency-ordered series

### 1. Persistence, safety, and presentation-neutral contracts

Own the backward-compatible configuration types and atomic settings writes, typed user-action outcomes, diagnostic scrubbing, queue presentation seams, and the narrow tests for those contracts.

Primary paths:

- `Source/FileManager/`
- `Source/LibationFileManager/ContemporaryExperienceSettings.cs`
- contemporary members in `Source/LibationFileManager/Configuration.PersistentSettings.cs`
- `Source/LibationUiBase/Diagnostics/`
- the small presentation seams in `Source/LibationUiBase/ProcessQueue/`
- matching focused tests under `Source/_Tests/`

Why first: later UI PRs depend on safe persistence, typed results, and scrubbed error detail, while this layer has no Avalonia layout dependency.

### 2. Design-system foundations and licensed assets

Own semantic profiles, palettes, tokens, reusable controls, vector sources, bundled typography and license, raster provenance, platform icon inputs, and preview scoping. Keep feature-specific view models out.

Primary paths:

- `Source/LibationAvalonia/DesignSystem/`
- `Source/LibationAvalonia/Assets/`
- platform icon inputs under `Source/LoadByOS/` and `Source/LibationWinForms/`
- `docs/design/asset-*`, `docs/design/components.md`, and `docs/design/design-tokens.md`

Depends on: PR 1 configuration types.

### 3. Default-off shell, routing, onboarding entry, and capture harness

Own the transactional host switch, route model, responsive shell, current-interface escape hatch, profile chooser entry, inert capture mode, demo-profile tooling, and capture-plan parser/driver. Route bodies may be placeholders backed by the real owners until their PR lands.

Primary paths:

- `Source/LibationAvalonia/Shell/`
- shell-hosting changes in `App.*`, `MainWindow.*`, native menus, and settings entry points
- `Source/LibationAvalonia/Diagnostics/CapturePlan.cs`
- `Scripts/demo-profile.cs`, `Scripts/seed-demo-*`, `Scripts/capture-*`
- shell, activation, and capture-plan contracts

Depends on: PRs 1-2.

### 4. Library, Current Flight, and Overview

Own the shared Library details/gallery projection, cover cache, cancellable filtering, stable-ID Flight service and tray, dashboard projection, and the two Overview compositions. Keep all operations delegated to existing `MainVM` commands.

Primary paths:

- `Source/LibationAvalonia/Features/Library/`
- `Source/LibationAvalonia/Features/Flight/`
- `Source/LibationAvalonia/Features/Overview/`
- their narrowly required `MainVM` / `ProductsDisplay` adapters
- related contract and performance tests

Depends on: PR 3 shell and shared components.

### 5. Processing, Decanter, secondary destinations, and onboarding completion

Own the single-queue Processing projection and re-parented Decanter plus Downloads, History, Accounts, Settings, Tools, Trash, and completed onboarding route actions. Split a destination into a follow-up only if maintainer review finds a domain-owner change that is independently mergeable; do not split presentation from the command adapter it requires.

Primary paths:

- `Source/LibationAvalonia/Features/Processing/`
- the remaining `Source/LibationAvalonia/Features/` destinations
- narrowly required dialog and command-adapter changes
- destination-content and ownership tests

Depends on: PRs 3-4 for shell hosts, Library activation, and Flight actions.

### 6. Variant, localization, regression, and release hardening

Own the final localized resource sweep, variant/accessibility assertions, committed visual baselines, large-library/performance contracts, cross-platform capture export, workflow hardening, packaging corrections, rollback proofs, and evidence-based release/status documents.

Primary paths:

- `Source/LibationAvalonia/Properties/Resources.*`
- S7-S9 contract tests and `Baselines/`
- `.github/workflows/`
- packaging scripts
- `docs/design/contemporary-cellar-status.md` and release evidence records

Depends on: the complete visible surface from PRs 1-5. This PR supplies regression and packaging proof; it does not change the default or claim rollout approval.

## Review and landing policy

For each re-cut, publish a range-diff from the relevant development commits to the clean upstream series and list any intentional omissions. Land in order. If an earlier PR changes during review, rebase and re-prove only its downstream dependents. Do not publish packages or offer the contemporary shell to users merely because the source series lands; release admission remains a separate owner decision backed by the package and supported-platform evidence matrix.
