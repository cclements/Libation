# S5 Processing and The Decanter design QA

- Binding source references:
  - `/Users/chris/projects/libation-patch/libation-contemporary-cellar-complete-agent-pack/02-reference-mockups/01-cellar-main-dashboard.png`
  - `/Users/chris/projects/libation-patch/libation-contemporary-cellar-complete-agent-pack/02-reference-mockups/02-tasting-room-main-dashboard.png`
- Source pixels: 1448 × 1086 for each supplied board, including its decorative window frame.
- Final implementation captures: `/Users/chris/projects/libation-patch/runtime-audit-2026-09-02/S5/captures/r10/`; expanded failed-row evidence: `captures/failed-focus-r3/`.
- Implementation viewports: 1456 × 1060 and 960 × 720 logical pixels at 2× density.
- Candidate identity: apphost `86ec0a78215004e853e28c4c940270666a47641ca7b40cf4e2a72876c09b55b3`; `Libation.dll` `54d0eafe3270de15fff01066d4ae3708f215f2586677528bcaddd39941bcfc5a`; `LibationUiBase.dll` `77321644a9b6c69304f29b85ee8e701999271233b229746ca4b86a0023b158b7`.
- Implementation state: isolated 1,000-title demo data with capture-only empty and mixed queue projections. The mixed fixture contains one active, one waiting, one completed, and one failed item without starting the production queue runner. The failed `Return to Meridian` row is currently not liberated, retains a download/decrypt recipe, and therefore truthfully exposes Retry.

## Same-input comparison evidence

Each listed image contains the supplied reference and matching implementation in one comparison input. All six were inspected at original density, together with all sixteen individual final frames.

- Cellar Overview / mixed Decanter: `runtime-audit-2026-09-02/S5/comparisons/r10/cellar-overview-mixed-v-reference.png`.
- Tasting Room Overview / mixed Decanter: `runtime-audit-2026-09-02/S5/comparisons/r10/tastingroom-overview-mixed-v-reference.png`.
- Cellar Processing / mixed queue: `runtime-audit-2026-09-02/S5/comparisons/r10/cellar-processing-mixed-v-reference.png`.
- Tasting Room Processing / mixed queue: `runtime-audit-2026-09-02/S5/comparisons/r10/tastingroom-processing-mixed-v-reference.png`.
- Cellar Decanter focused comparison: `runtime-audit-2026-09-02/S5/comparisons/r10/cellar-decanter-focus-v-reference.png`.
- Tasting Room Decanter focused comparison: `runtime-audit-2026-09-02/S5/comparisons/r10/tastingroom-decanter-focus-v-reference.png`.

## Acceptance matrix

The final run produced all sixteen required states with no missing frame:

- Processing and Overview/Decanter;
- empty and mixed queue;
- Cellar and Tasting Room; and
- Wide 1456 × 1060 and Compact 960 × 720.

All eight Wide screenshots are exactly 2912 × 2120 pixels. All eight Compact screenshots are exactly 1920 × 1440 pixels. Compact Cellar evidence opens the existing Decanter drawer; Compact Tasting Room retains the card in the Overview composition.

The supplemental failed-row run produced four more exact-size frames: two Wide at 2912 × 2120 and two Compact at 1920 × 1440. Each starts the failed `Return to Meridian` row expanded and keeps Copy details, Retry, and Open log visible.

## Findings and corrections

No P0, P1, or P2 visual finding remains.

- **P1, corrected:** the shell-owned Current Flight could render blank after a profile/route re-parent because its parent-sensitive one-time data-context binding had already resolved. The shell now applies the retained Flight owner immediately before every host attachment. Final Wide Cellar frames show the expected Flight rows, while the Processing route truthfully shows an empty Flight after the capture seam clears selection.
- **P2, corrected:** Compact Cellar exposed a second external “Open full Processing workspace” action around the Decanter drawer in addition to the Decanter's own action. The duplicate shell affordance was removed; the final drawer has one canonical Open Processing action.
- **P2, corrected:** an earlier Tasting Room Decanter composition overflowed its available card space. The final layout keeps the supplied illustration, current title, stage, progress, and conditional action inside the card at both binding sizes.
- **P2, corrected:** the first failed-row composition did not keep the literal owner failure and recovery guidance legible at the required density. The final expanded failure row keeps the literal “Disk full, queue stopped” summary, recovery guidance, and correlation reference in the normal visual and accessibility trees.
- **P1, corrected:** a tightened production retry predicate exposed that the earlier capture fixture used an already-liberated title for its failed row, so Retry correctly disappeared. The final fixture uses the current not-liberated `Return to Meridian` title with a retained decrypt recipe. Retry is visible, unobscured, and spatially distinct in both profiles at both binding sizes.

An independent final visual pass found no visible S5 P0/P1/P2 blocker across the sixteen final frames, four supplemental failed-row frames, six comparison sheets, and four comparison crop sources. It specifically confirmed the populated Cellar Flight, removal of the duplicate Compact action, corrected Tasting Room overflow, and legible Retry action in all four expanded failure states.

## Intentional reference differences and proof boundary

- The Processing route has no dedicated supplied mockup. It therefore extends the accepted profile tokens, type, spacing, cards, glyphs, and shell hierarchy without inventing a second design system.
- Reference and demo title data differ. The accepted match is hierarchy, density, tone, profile distinction, ownership, and state behavior—not fabricated production content.
- The supplied board frame is presentation context, not part of the application viewport.
- These captures prove the named local macOS rendered states. They do not prove VoiceOver or another assistive technology, 200% logical scaling, runtime High Contrast or reduced motion, Windows/Linux, installed packaging, notarization, distribution, publication, or release.

final result: passed
