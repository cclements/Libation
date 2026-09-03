# Contemporary Cellar workstream status

> Re-planned 2026-09-01. The rows below describe source integration only and are superseded by the
> evidence-based slice records in the libation-patch records repository
> (`docs/reviews/2026-09-01-contemporary-cellar-gap-review.md`,
> `docs/superpowers/specs/2026-09-01-contemporary-cellar-replan-design.md`). A slice is complete only
> when its capture review is marked Passed.

## Evidence-based slice ledger

| Slice | State | Admitted evidence | Remaining boundary |
|---|---|---|---|
| S0 — verification harness | Complete | Isolated 1,000-title demo profile; deterministic capture-plan parser/driver; 60 exact-size Cellar/Tasting Room route captures; contact-sheet generator; SHA-256 evidence set. Source commits `f5fa4888` through `da8821d4`; records commit `fe73761`. | Static/local macOS harness evidence only; it does not prove interaction, assistive technology, another OS, packaging, or release behavior. |
| S1 — design-system hardening | Complete; visual review Passed | Contemporary styling reaches the real Library details grid; stateful navigation, filters, progress, MetricCard, and semantic Liberate status presentation; corrected Tasting Room contrast; bundled Source Serif 4 under OFL-1.1; effective reduced-motion resources; complete decoration gates; lazy Overview profile creation; downsampled/provenance-recorded rasters; product-copy sweep; DEBUG gallery; stable profile-switch capture host. Full Release source gate: 1,774 discovered, 1,751 passed, 23 platform skips, 0 failed. Release and Debug Avalonia builds: 0 warnings, 0 errors. Twelve isolated macOS captures at exact 2x wide/compact dimensions were inspected beside the supplied boards/mockups. Evidence: outer `runtime-audit-2026-09-01/S1/` and `docs/reviews/2026-09-01-S1-design-system-review.md`. | The S1 screenshots prove the named rendered states only. Keyboard/focus traversal, VoiceOver, 200% logical scaling, High Contrast runtime, reduced-motion animation behavior, Windows/Linux, installed packaging, and release remain unproven. S3 owns full Library modernization; S4 owns the dashboard hierarchy/composition differences visible against the supplied mockups. |
| S2 — shell recomposition | Complete; visual and interaction review Passed | One shell-owned route header; unified primary/utility navigation; SplitView Wide/Compact/Narrow presentations; single re-parented Flight and Decanter surfaces; shell status bar; responsive toolbar; active-only keyboard/native-menu routing; disposed/recreated opt-out graph. Full Release solution gate: 1,774 discovered, 1,751 passed, 23 platform skips, 0 failed. Release and Debug Avalonia builds: 0 warnings, 0 errors. Sixty isolated exact-size macOS captures and twenty same-input comparison sheets verified by SHA-256; attended keyboard/focus traversal passed in both profiles at all three layout classes; runtime opt-out restored Classic content and bindings. Evidence: outer `runtime-audit-2026-09-02/S2/` and `docs/reviews/2026-09-02-S2-shell-recomposition-review.md`. | Local macOS source/runtime evidence only. VoiceOver and other assistive technology, 200% logical scaling, runtime High Contrast/reduced motion, Windows/Linux, installed packaging, notarization, publication, and release remain unproven. S3 owns full Library and Current Flight composition; S4 owns Overview; S5 owns Decanter contents; S6 owns secondary-route bodies. |
| S3 — Library and Current Flight | Complete; visual and interaction review Passed | One responsive Library command surface; shared Details/Gallery stable-ID Flight selection; worker/cancelled latest-request filtering; synchronized sort projection; responsive details pane; diff-updated Flight rows and coalesced persistence. The exact-final-source Release solution gate passed 1,774 total with 1,751 passed, 23 platform skips, and 0 failed. Release and Debug Avalonia builds pass with 0 warnings/errors. Nine exact-size selected-state macOS captures and nine comparison sheets cover both profiles and both Library presentations; attended interaction passed selection, details, filtering, sorting, route persistence, and Clear/undo. A separate exact-candidate selected-to-Clear capture renders `Current Flight is empty` and `Select titles in the Library to build a Flight`. Evidence: outer `runtime-audit-2026-09-02/S3/` and `docs/reviews/2026-09-02-S3-library-current-flight-review.md`. | Local macOS source/runtime evidence only. Gallery-checkbox assistive behavior, VoiceOver, scaling, runtime High Contrast/reduced motion, Windows/Linux, packaging, notarization, publication, and release remain unproven. S4 owns Overview; S5 owns Decanter contents; S6 owns secondary-route bodies. |
| S4 — Overview | Complete; source, automated, visual, and interaction review Passed | One shell-lifetime dashboard projection now feeds distinct Cellar and Tasting Room compositions while retaining the S3 Library/Flight and existing processing owners. Recent rows are bounded to ten and carry product IDs; Current Flight processing joins use product ID; invalidation is coalesced behind the accepted 300 ms debounce. The approved focused Overview packet passes 4/4. The unchanged full Release solution gate passes 1,780 total with 1,757 passed, 23 expected platform skips, and 0 failed. Release and Debug Avalonia builds pass with 0 warnings/errors. Sixteen exact-size isolated macOS captures cover both profiles at Wide/Compact across populated, processing, empty-library-with-account, and no-account states; final same-input comparison sheets pass. Attended traversal passes both profile search/open paths, View Processing, Browse Files cancellation, profile switching, and live Wide/Compact reflow. Evidence: outer `runtime-audit-2026-09-02/S4/` and nested `design-qa.md`. | Local macOS source/runtime evidence only. VoiceOver and other assistive technology, 200% logical scaling, runtime High Contrast/reduced motion, Windows/Linux, packaging, notarization, publication, and release remain unproven. S5 owns Decanter contents and S6 owns secondary-route bodies. |

The historical prompt ledger below is retained as the source-integration record.
Its earlier “unverified” statements are superseded only where the slice ledger
above names newer admitted evidence; no broader rollout claim is implied.

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
