# Contemporary Cellar evidence ledger

Updated 2026-09-03. This ledger records only evidence admitted for the exact source named in each row. It does not turn source, test, capture, package, or fork delivery into rollout approval.

Binding plan: `docs/superpowers/specs/2026-09-01-contemporary-cellar-replan-design.md` in the records repository. Current development branch: `contemporary-cellar-v2` on the `cclements/Libation` fork. `UseContemporaryShell` remains default-off.

## Slice status

| Slice | State | Exact source and admitted evidence | Remaining boundary |
|---|---|---|---|
| S0 — verification harness | Complete | Source commits `f5fa4888` through `da8821d4`; records commit `fe73761`. Isolated 1,000-title demo profile, deterministic plan parser/driver, 60 exact-size macOS captures, contact-sheet generator, and verified SHA-256 set. | Harness and macOS rendered-state evidence only; no supported-platform, accessibility, installed-package, or release proof. |
| S1 — design-system hardening | Complete; visual review Passed | Source `f24d2ea4`; records `d842ee4`. Full Release: 1,751 passed, 23 expected skips, 0 failed; Release and Debug builds: 0 warnings/errors. Twelve exact-size isolated macOS captures inspected against the supplied references. | Named macOS states only; no manual assistive-technology, Windows/Linux, package, or release proof. |
| S2 — shell recomposition | Complete; visual and interaction review Passed | Source `276b78ed`; records `959493f`. Full Release: 1,751 passed, 23 expected skips, 0 failed; Release/Debug: 0 warnings/errors. Sixty isolated captures, twenty comparisons, and attended keyboard/focus traversal across both profiles and three layout classes. Runtime opt-out restored Classic content and bindings. | No VoiceOver, Windows/Linux, installed-package, notarization, publication, or release proof. |
| S3 — Library and Current Flight | Complete; visual and interaction review Passed | Source `fde09e2f`; records `4aa96bf`. Full Release: 1,751 passed, 23 expected skips, 0 failed; Release/Debug: 0 warnings/errors. Nine selected-state captures/comparisons plus exact-candidate empty-state and attended selection, filtering, sorting, route persistence, Clear, and undo. | Gallery-checkbox assistive behavior, Windows/Linux, package, notarization, publication, and release remain unproved. |
| S4 — Overview | Complete; source, automated, visual, and interaction review Passed | Source `d27bd762`; records `5c77b1e`. Focused Overview 4/4; full Release 1,757 passed, 23 expected skips, 0 failed; Release/Debug: 0 warnings/errors. Sixteen exact-size state captures and attended search/open, Processing navigation, file-picker cancellation, profile switching, and reflow. | No assistive-technology, Windows/Linux, package, notarization, publication, or release proof. |
| S5 — Processing and Decanter | Complete; source, automated, visual, and scoped interaction review Passed | Source `3650bba7`; records `95d3a7c`. Focused Processing 5/5; full Release 1,762 passed, 23 expected skips, 0 failed; Release/Debug: 0 warnings/errors. Twenty exact-size captures, reference/density review, and safe interaction with Performance, queue log, profile chooser, Copy details, and Open log. | Live retry/cancel results, assistive technology, Windows/Linux, package, notarization, publication, and release remain unproved. |
| S6 — secondary destinations and onboarding | Complete; source, automated, visual, and scoped interaction review Passed | Source `e1235f34`; records `ede1812`. Focused destinations 8/8; full Release 1,777 passed, 23 expected skips, 0 failed; Release/Debug: 0 warnings/errors. Twenty-nine verified captures and isolated interaction for filters, deep links, appearance transaction, cancel-default actions, and newest-three Flight. | No live owner-action success, assistive-technology, Windows/Linux, installed-package, notarization, distribution, or release proof. |
| S7 — variant matrix | Complete; source, automated, visual, and accessibility-tree review Passed | Source merge `b8e16b4b`; records `55996a3`. Focused matrix 11/11; full Release 1,804 passed, 23 expected skips, 0 failed; Release build: 0 warnings/errors. Twenty-seven verified captures cover Compact, High Contrast, Decoration Off, reduced motion, and 200% logical scaling; headless peers cover focus visibility and accessible names. | Automated peers are not manual VoiceOver. Windows/Linux rendering, installed packages, notarization, distribution, publication, and release remain unproved. |
| S8 — tests and evidence | Complete; source, automated, headless-render, and deterministic-fixture review Passed | Source `09b6c5b0`; records `962fd0d`. Deterministic 1k/10k/50k fixtures passed distinct-ID and SQLite integrity checks. Focused S8 plus destination content 14/14; full Release 1,810 passed, 23 expected skips, 0 failed. Two 960 x 720 Skia baselines were non-empty and pixel-compared inside the profile scope. | Local macOS source/headless evidence only; no Windows/Linux render, installed-package, notarization, distribution, publication, or release proof. |
| S9 — release hardening | In progress; local automation Passed | Starting source `09b6c5b0`, fetched `origin/master` `3d563a2d`; branch was 27 ahead and 0 behind. Scope and upstream split proposal drafted. Focused S2/S8/S9 gate: 10 passed, 0 failed/skipped. Unchanged full Release gate: 1,812 passed, 23 expected skips, 0 failed across all nine assemblies. The existing live opt-out contract plus two new cases prove invalid-setting fallback and downgrade/legacy-writer preservation. | Windows/Linux runner builds and inspected captures, package matrix, exact-source delivery, and closeout remain open. Local macOS automation does not prove another OS or a package. |

## Current rollout disposition

**NO-GO for rollout.** The contemporary shell remains experimental and default-off. S0-S8 establish the named local source, automated, rendered-state, and interaction contracts; they do not establish supported-platform package behavior or owner release admission.

| Stage | State | Evidence still required to advance |
|---|---|---|
| experimental opt-in | Implemented, default-off | Complete S9 and disposition any supported-platform/package defect. |
| stable opt-in | Not authorized | S9-supported packages plus owner-approved accessibility and beta criteria. |
| one-time existing-user offer | Not enabled | Stable opt-in evidence and explicit owner approval. |
| default for new installs | Not enabled | Supported-platform beta evidence and explicit owner approval. |
| remove current interface | Out of scope | Separate major-release decision. |

## Evidence rules

- A source or compile pass is not runtime or visual proof.
- A headless image is not an installed-package launch.
- A workflow artifact is not publication, notarization, distribution, or release.
- A fork branch or fork-internal CI pull request is not an upstream pull request or maintainer contact.
- A successful S9 packet will close only the S9 contract. Rollout state changes require a separate owner decision.
