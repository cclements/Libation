# Planning, verification and delivery

Use one bounded change with a clear user outcome, a known owner and evidence appropriate to its risk. This procedure connects routine implementation to audio correctness, interface usability and the exact packages delivered. It does not require a full audit or all-platform run for every edit.

The active task and current repository instructions determine scope. When working in the multi-repository development workspace, start with `DEVELOPMENT.md`: the audio/integration roadmap is `DEVELOPMENT-PLAN.md`, while the interface progress ledger owns IC packet status. `HANDOFF.md` and `AUDIO-REVIEW-HANDOFF.md` retain their respective continuation details. In a standalone checkout, use the active issue/plan instead; the surrounding workspace is not a build dependency. Dated S0-S9 and audio correction receipts are evidence for their recorded sources, not current acceptance of new work.

For detailed UI target, implementation and closure rules, follow the [interface workflow](interface-workflow.md). This procedure adds the general/audio/native/package impact model and shared integration checks; it does not replace the interface workflow or duplicate its live status ledger.

## Start a change

Read the current instructions and relevant plan, inspect Git/dirty state, identify the actual dependency graph and preserve unrelated work. Pick the smallest useful contract: an observable trigger, expected behavior, affected owner and meaningful acceptance check. Do not reopen settled choices unless a concrete conflict or new fact changes them.

Record a short work item in the active plan or issue:

```text
Packet / finding IDs:
Problem and intended behavior:
Owned repositories/files; exact base(s); dependencies:
Acceptance examples and required evidence:
Implementation and observed validation results:
Open decisions/limitations; source vs integration/delivery status:
Next bounded action:
```

Fill result fields after observing the result. For larger changes, link detailed logs/manifests rather than expanding this into a second report. A new architectural decision needs a short ADR when it changes ownership, public API/ABI, persisted data, output semantics or a platform contract; routine corrections within an established contract do not need a new ADR.

Routine local implementation and proportionate checks proceed under the active implementation request. Documentation-only planning does not itself start product implementation. Ask only when an unresolved choice materially changes the outcome, or an external/destructive/costly action is outside existing authorization. Prepare the concrete candidate or decision examples first; elapsed time does not supply approval.

## Implement through existing owners

Libation's presentation layers consume the existing library, settings, selection and processing owners. Keep one command/queue/settings path across Avalonia, Classic and CLI consumers. A UI change may recompose the view; it must not silently add a second download engine or redefine output completion.

Audio changes preserve compressed content for remux and distinguish source format, requested settings, effective decoded PCM and final output. Validate bounds and unsupported profiles explicitly. Treat packet/handle ownership, delayed output, encoder priming, sync/preroll, chapter intervals, resumed object identity and output publication as contracts with failure cases.

Use isolated worktrees/package caches where another workstream or incompatible dependency graph is active. Port accepted fixes by behavior and tests onto the selected current base. Keep native/managed ABI pairs coherent; do not overwrite an immutable public package coordinate with local changes. Pin native source/toolchain/configuration and record artifact identity when that path changes.

## Select verification by impact

Choose the smallest meaningful checks during implementation, then the broader affected checks once the candidate is stable. Tests should detect a real failure, not mirror field assignments or inflate a count. One evidence layer cannot stand in for another.

| Change touches | Required evidence for its claim | Add when applicable |
|---|---|---|
| Documentation only | Document/source/link/diff inspection; preserve existing receipts | No app/native/website build, test or test-bearing gate merely for prose |
| Pure parser, timing, metadata or chapter logic | Boundary/malformed/round-trip regressions and affected build; exact selected test identities/counts | Independent output parse/decode when bytes or presentation change; cumulative and long-file boundaries |
| Native buffers, handles, decode or encode | Actual pinned payload/source; allocation/lifetime/sanitizer checks; delayed/EOF output and sample/signal cases | Affected RIDs; encoder delay/DRC/effective format; ABI/package consumer; rebuild all RIDs affected by shared builder/config changes |
| HTTP, license or output lifecycle | Synthetic authenticated contracts/local HTTP/fault injection; cancellation/restart/cleanup and failure propagation | Disposable real-media/provider journey only for claims that depend on it; protect existing files and retain recovery evidence |
| UI layout, controls or interaction | Affected Avalonia build; meaningful bounds/state/lifecycle contracts; inspect actual relevant frames and interactions against an admitted target | Keyboard/focus/accessibility, text/OS scale, native surfaces and installed platforms affected by the change |
| Settings or persisted/output semantics | Existing-data/migration/draft/cancel/restart and rollback/recovery cases | CLI/Classic/Avalonia parity, old output/resume journal compatibility, user decision for destructive behavior |
| Managed dependencies or native packaging | Isolated Release consumer; exact restore/package/publish identities and applicable behavioral regressions | Native exports/config/license/advisory disposition and installed artifact verification; source Debug proof is insufficient |
| Performance, quality or new capability | Fixed fixture/host baseline; before/after measured behavior and named profile/player support | Blinded listening, independent aligned decode, loudness/peak/clipping, memory/temp/disk costs as appropriate |

Use [Testing changes](testing.md) for repository commands and fixtures, [UI evidence](ui-testing.md) for captures/interactions, and the active audio standards/fixture ledger for codec claims. Libation uses Microsoft.Testing.Platform: `dotnet test --project <project.csproj>`. Sibling libraries may use a different runner; inspect their current projects/instructions before copying commands. An exit-zero run selecting no intended tests is not a pass.

Tests/captures have bounded timeouts. Hermetic tests use synthetic/local inputs and isolated profiles, without a real account or secret store. Verify that `--no-build` uses the intended binary. Record expected skips and missing hardware/fixtures explicitly; an unavailable test is neither a pass nor an observed product defect. Follow current dependency-advisory handling rather than suppressing warnings globally.

## Review and integrate

Inspect the complete owned diff for the actual trigger, adjacent callers, public API/ABI compatibility, overflow/lifetime/failure paths and unchanged domain ownership. Use independent review when required by the active task or when a high-risk contract warrants it and the task permits the reviewer; routine documentation and low-impact changes need proportionate self-review. No delegation is implied by this procedure.

Reconcile any concurrent changes before integration. Run the checks invalidated by a changed source, dependency, compiler/toolchain, native artifact, configuration or target; retain unchanged bounded receipts with exact identity. Do not repeatedly run an unaffected full suite just to make the report longer.

Each new regression belongs in the relevant maintained test suite. As its harness becomes runnable, add it to the appropriate CI lane. Keep hermetic, native/RID, package-consumer, licensed-media/provider and installed-platform lanes distinct; never make a licensed title or private account a hidden prerequisite for ordinary unit tests.

The current Libation validation workflow builds/tests through `.github/workflows/validate.yml` and `build.yml`. Planned additional audio/native/package gates are tracked in the workspace A6.1 packet; this guide does not claim those gates already exist. Package workflow success must include actual publish/retrieval evidence before reporting delivery.

## Close the loop with the product

For audio/domain changes consumed by the interface, verify the full relevant journey: requested settings → selected eligible source → queue/progress → validated output/publication → final metadata/history. Include failure, cancellation, retry and restart. Inert UI state proves presentation only; it cannot prove a successful provider/download/conversion operation. Native menus, text/scale and assistive technology need the appropriate platform evidence.

Store a compact receipt with exact sources and dirty scope, fixture/target identity, commands and observed names/counts/skips, relevant package/native/output hashes, accepted images for visual changes, and unresolved limits. Do not put credentials, keys, signed URLs, license responses or private account details into receipts. Use the contributor [logging and secrets guidance](contribute.md#logging-and-secrets).

Report separate states: source verified, integrated into the intended package, accepted for a named runtime/profile/platform, and delivered. Update the active plan's finding/surface row and current handoff in the same closeout. Keep historical receipts unchanged; if later evidence narrows or invalidates a claim, append the reason and new result.

## Final candidate and delivery

Freeze the exact source/dependency/native graph for the intended candidate and run the broader applicable audio, interaction, accessibility and installed-platform checks once it is stable. Re-run affected proof after any subsequent material change. An audio maintenance release can have its own bounded acceptance without requiring a separate experimental-interface rollout.

For changed audio packages, integrate and verify in dependency order: AAXClean, then Codecs against the intended parser package, then Libation against Codecs. Use unique isolated development versions locally; official versions must be unused and owner-controlled. Retain exact native source/configuration/ABI/distribution records. Verify transformed publish payloads through the build chain rather than assuming raw package hashes remain unchanged after ReadyToRun or signing.

Release, normal-environment installation, signing/notarization, publication, maintainer contact and rollout require their applicable authorization; local checks do not grant it. Before such a request, finish the concrete candidate and record its exact target, effect and remaining risk. Keep rollback/recovery artifacts and preserve user books/settings. No support, performance, ISO-conformance or rollout claim should exceed its observed evidence.
