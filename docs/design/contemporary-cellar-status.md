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
| S9 — release hardening | Complete at source, automation, headless-render, and package-artifact tiers | Source `0f06ce97` plus CI portability correction `d78737f8`. The original S9 focused gate passed 10/10 and the unchanged local Release gate passed 1,812 with 23 expected skips and 0 failures. At exact source `d78737f8`, fork-only validation run `33826389731` passed every unit-test, publish, bundle, and artifact step across three Windows, two macOS, and four Linux jobs. It produced nine non-release packages and two capture artifacts. Both Cellar and Tasting Room 960 x 720 captures from Windows x64 and Debian x64 were downloaded, hashed, and visually inspected. | Headless captures and CI artifacts are not installed-package launches. Manual assistive-technology, signing/notarization, distribution, publication, beta, and rollout proof remain open. |

## Current rollout disposition

**NO-GO for rollout.** The contemporary shell remains experimental and default-off. S0-S9 establish the named source, automated contracts, Windows/Linux headless rendering, and a supported-platform non-release package-artifact matrix. They do not establish installed-package behavior, manual assistive-technology behavior, signing/notarization, beta acceptance, or owner release admission.

| Stage | State | Evidence still required to advance |
|---|---|---|
| experimental opt-in | Implemented, default-off | S9 complete at the admitted tiers; retain while installed-package and accessibility evidence is absent. |
| stable opt-in | Not authorized | Owner-approved installed-package, accessibility, and beta criteria. |
| one-time existing-user offer | Not enabled | Stable opt-in evidence and explicit owner approval. |
| default for new installs | Not enabled | Supported-platform beta evidence and explicit owner approval. |
| remove current interface | Out of scope | Separate major-release decision. |

## Evidence rules

- A source or compile pass is not runtime or visual proof.
- A headless image is not an installed-package launch.
- A workflow artifact is not publication, notarization, distribution, or release.
- A fork branch or fork-internal CI pull request is not an upstream pull request or maintainer contact.
- S9 completion closes only the release-hardening contract at the recorded tiers. Rollout state changes require a separate owner decision.
