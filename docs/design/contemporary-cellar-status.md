# Contemporary Cellar workstream status

Effective delivery baseline: `3e7191adc7f41f1dec252b95e505b3f318be3b34`.
Measured UI intake baseline: `094e207c0b245f36592ce31000f693674b886057`.
Integration branch: `codex/contemporary-cellar`.
Integration worktree: `/Users/chris/projects/libation-patch/Libation`.

This ledger distinguishes implemented source from evidence that has not been
authorized or reproduced. “Complete” below means the prompt's source integration
is present; it does not imply runtime, package, supported-platform, or rollout
admission.

The supplied records do not retain individual worker identities, so
**Owner/agent** names the accountable prompt role plus Prompt 00 fan-in rather
than inventing a person. **PR scope** records the pack's logical review boundary;
the integrated source is committed on `codex/contemporary-cellar`, rebased onto
the effective delivery baseline, and designated for the `cclements` fork only.
No pull request is open or authorized. The final macOS Release compilation
succeeded with 0 warnings and 0 errors, and the focused settings, persistence,
and diagnostic-scrubbing packet passed 23 selected cases. Those results do not
satisfy interaction, accessibility, performance, package, supported-platform,
or rollout gates.

| Prompt | Workstream | Owner/agent | Prerequisite | Branch/worktree | PR scope | State | Test state | Visual verification state | Asset dependencies | Blocker |
|---|---|---|---|---|---|---|---|---|---|---|
| 01 | Baseline and ADR | Prompt 01 role; Prompt 00 fan-in | None | `codex/contemporary-cellar` at the integration worktree | Logical PR 1; none opened | Source integrated; baseline, ADR, command inventory, and visual-intake record present | No baseline-specific tests run | Reference intake verified; isolated current-interface runtime baseline remains unmeasured | Reference PNGs are evidence only; no shipping asset dependency | Baseline runtime and benchmark evidence absent |
| 02 | Tokens and profiles | Prompt 02 role; Prompt 00 fan-in | 01 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 2–4; none opened | Source integrated; semantic profiles, persistence, resolver, and preview scope present | 11 dictionary/settings cases passed; no Avalonia profile runtime tests | Reference and scoped-preview source review recorded; runtime switching, Follow System, restart persistence, and fallback unverified | Semantic dictionaries and verified boards; no raw reference pixels | Runtime profile behavior remains unverified |
| 03 | Brand, glyphs, and illustrations | Prompt 03 asset owner; Prompt 00 fan-in | 02; stable token and asset IDs | `codex/contemporary-cellar` at the integration worktree | Logical PR 24 and shared PR 25; none opened | Source asset contract integrated; required brand, glyph, status, functional-illustration, and platform-icon sources present | No asset-runtime tests run | Manifest, individual SVG exports, file/frame checks, and contact sheet reviewed; Avalonia DPI and installed rendering unverified | Sole owner of `brand.*`, `glyph.*`, `status.*`, `illustration.*`, and platform icon exports; three decorative IDs explicitly deferred | Supported-platform runtime and installed-artifact review absent |
| 04 | Shared components | Prompt 04 role; Prompt 00 fan-in | 02; Prompt 03 asset IDs | `codex/contemporary-cellar` at the integration worktree | Logical PR 5 and shared PR 25; none opened | Source integrated; shared controls consume semantic resources and stable asset IDs | No component tests run | Source/component-state review only; focus, scaling, High Contrast, Decoration Off, and assistive behavior unverified | `AssetResources.axaml` plus shared glyph, status, brand, and functional-illustration IDs | Runtime component-state and accessibility matrix absent |
| 05 | Shell and navigation | Prompt 05 role; Prompt 00 fan-in | 02 and 04 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 6–8; none opened | Source integrated; one route service, disabled shell host, current-interface rollback path, and legacy owners embedded | No shell/navigation tests run | Source integration only; responsive rail, shortcuts, focus restoration, native menus, and runtime rollback unverified | Route glyphs, brand mark, shared shell components, and semantic profiles | Isolated navigation, keyboard, active-processing, and rollback interaction evidence absent |
| 06 | Overview profiles | Prompt 06 role; Prompt 00 fan-in | 04 and 05 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 9–11; none opened | Source integrated; shared aggregation contract and both profile compositions present | No Overview tests run | Source compositions reviewed; required profile, state, and responsive capture matrix unverified | Shared metrics, status marks, route glyphs, and optional functional illustrations | Live-data, responsive, keyboard, and screenshot comparison evidence absent |
| 07 | Library modernization | Prompt 07 role; Prompt 00 fan-in | 04 and 05 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 12–14; none opened | Source integrated; existing Details grid retained, Gallery projection/cache added, and one Flight selection seam used | No Library/performance tests run | Structural source review only; large-library, keyboard, focus, screen-reader, and visual behavior unverified | Library, filter, selection, status, cover-placeholder, and empty-state assets | Deterministic 10k/50k fixture and runtime performance/accessibility evidence absent |
| 08 | Current Flight | Prompt 08 role; Prompt 00 fan-in | 07 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 15–17; none opened | Source integrated; one shell-scoped stable-ID service, shared preflight, typed owner-action outcomes, and profile presentations present | Persisted Flight-ID normalization passed; Flight service/interactions untested | Source ownership review only; warning confirmation, cancellation/no-change presentation, undo, focus, drawer, and queue execution unverified | `glyph.flight`, process/output-profile glyphs, status marks, and shared components | Authorized interaction and cross-platform drawer evidence absent |
| 09 | The Decanter | Prompt 09 role; Prompt 00 fan-in | 04 and 05 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 18–19; none opened | Source integrated; one existing queue is projected; applicable retry, correlated/scrubbed failure detail, direct Open Log, and legacy controls remain reachable | 12 scrubber cases passed; queue interactions untested | Source ownership review only; cancel, retry, log routing, clear, large-queue, assistive, and cross-platform behavior unverified | Processing and queue glyphs, queue statuses, progress, and empty-state assets | Authorized live-queue recovery and supported-platform evidence absent |
| 10 | Secondary screens and onboarding | Prompt 10 role; Prompt 00 fan-in | 05; final onboarding composition depends on feature routes | `codex/contemporary-cellar` at the integration worktree | Logical PRs 20–23; none opened | Source integrated; Downloads, History, Accounts, Settings, Tools, Trash, About, command adapters, and onboarding host present | Settings defaults/round trip passed; onboarding/routes untested | Route/host source review only; first-run, manual re-entry, dialogs, gestures, privacy, and platform rendering unverified | Route glyphs, status marks, profile previews, empty states, and brand assets | Isolated first-run/manual walkthrough and supported-platform interaction evidence absent |
| 11 | QA, accessibility, and cross-platform | Prompt 11 QA owner; Prompt 00 fan-in | Continuous after 04; final matrix after 01–10 | `codex/contemporary-cellar` at the integration worktree | Logical PRs 26–27 plus shared PRs 25 and 28; none opened | Not admitted; the 23-case focused packet is not the required full matrix | Focused packet passed 23; full matrix not run | No in-app accessibility, visual-regression, performance, package, Windows, or Linux matrix admitted | Every profile/component/asset plus installed Windows, macOS, and Linux package outputs | Isolated configuration, deterministic performance fixture, and supported-platform environments absent |
| 12 | Release review and hardening | Prompt 12 release reviewer; Prompt 00 fan-in | All workstreams, especially admitted Prompt 11 evidence | `codex/contemporary-cellar` at the integration worktree | Logical PRs 28–29 and release work; none opened | Source review complete; rollout remains **NO-GO** and default-off | Focused packet passed 23; rollout gates remain untested | Default-off/current-interface rollback reviewed in source; runtime rollback and launch matrix unverified | Asset manifest and platform icon inputs are source-complete; installed package evidence remains open | Prompt 11 launch gates and explicit owner rollout authority absent |

## Shared decisions

- Feature flag: `Configuration.UseContemporaryShell`, default `false` and
  persisted last when enabling the new source graph.
- Persisted profile and presentation preferences live in the UI-agnostic
  `LibationFileManager` configuration and are mapped by the Avalonia
  `ExperienceManager`.
- Current fallback name: `CurrentAvalonia` internally; “Current Libation
  interface” in user-facing copy.
- Route model: one Avalonia-layer route service; existing commands remain with
  their current owners and are reached through adapters.
- Selection model: one shell-scoped stable-ID `FlightService`; Details and
  Gallery selection are views over that service, not independent batch state.
- Queue model: the existing shared `ProcessQueueViewModel` remains authoritative;
  Decanter and overview surfaces project it rather than creating a queue.
- Current Flight presentation: profile-specific Overview surfaces own their
  Flight composition on Overview; the shell owns the persistent pane or drawer
  on other routes. Both bind the same service/view model.
- Semantic status model: current domain/queue status remains authoritative;
  presentation maps it to semantic status tokens and non-color text.
- Screenshot artifacts: reference images remain in the agent pack and do not
  ship. The generated asset contact sheet is source-review evidence, not runtime
  or package proof.

## Open evidence gates

| Gate | Why it remains open | Resolution condition |
|---|---|---|
| Safe isolated app interaction | Launching the current app can touch real user data, accounts, paths, and network state | Define an isolated no-account configuration and approve the exact interaction command/procedure. |
| Deterministic tests | The 23-case settings/persistence/scrubber packet passed, but no contemporary Avalonia headless fixture covers routes, Flight service behavior, resources, or controls | Obtain separate exact approval for any named remaining packet after defining the fixture and distinct evidence gap. |
| Accessibility and visual matrix | Compile/source inspection cannot prove focus, screen-reader output, contrast, scaling, or rendered fidelity | Run the named profile/state/viewport matrix in an isolated environment. |
| Large-library performance | No deterministic 10k/50k library fixture exists | Approve an exact fixture and measurement command with named markers and host. |
| Reduced-motion follow-system | Avalonia 12 exposes no single cross-platform system preference | Bind and verify platform adapters per supported OS; explicit On/Reduce remains deterministic. |
| Installed assets and supported platforms | A macOS source build does not prove packaged icons, launchers, native menus, or Linux/Windows rendering | Build and inspect named packages on each supported platform/environment. |
| Rollout | Runtime/platform evidence and owner authority are absent | Satisfy the release matrix, dispose defects, then obtain explicit rollout authority. |

## Intentional plan deviations

| Section | Deviation | Binding correction |
|---|---|---|
| Navigation shortcuts | `Cmd+1…8` collides with current quick filters | Preserve current macOS `Cmd+1…0` quick filters and use the non-colliding routed gestures recorded in the shell. |
| Fallback naming | `Classic` is ambiguous in this repository | Use `CurrentAvalonia`; do not rename the separate `Libation-Classic` application. |
| High contrast | Chardonnay is only a Light/Dark profile | Apply a separate semantic high-contrast palette without forking feature logic. |
| Preview | A preview harness already exists | Extend `ThemePreviewControl` with scoped resources instead of adding an app shell. |
| Processing | The queue is concurrent and may have multiple active jobs | Project the existing shared queue and aggregate its live state; do not duplicate execution. |
| Performance | Large fixtures are absent | Keep plan targets unproven until a reproducible authorized fixture exists. |
| Runtime launch | No isolated configuration contract is available | Do not use real library/account state as design verification evidence. |
