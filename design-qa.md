# S6 secondary destinations and onboarding design QA

- Binding design authority: the Cellar and Tasting Room boards plus the accepted S6 contract; no destination-specific mockups were supplied.
- Final implementation captures: `/Users/chris/projects/libation-patch/runtime-audit-2026-09-03/S6/captures/`.
- Implementation viewports: 1456 x 1060 and 960 x 720 logical pixels at 2x density.
- Candidate identity: apphost `86ec0a78215004e853e28c4c940270666a47641ca7b40cf4e2a72876c09b55b3`; `Libation.dll` `80d3d9ee0d76e03812ee180232be888e190018548da4a6ea50f8c6bd22b92911`; `LibationUiBase.dll` `a502af2506f0f5a08e08559d029c68bb864edf03802f8009463946fc1f47e331`.
- Implementation state: isolated 1,000-title demo data with one masked demo account, 19 removed records, an empty queue, and an empty capture Flight. Onboarding scan-active state is capture-only and inert.

## Acceptance matrix

The exact final run completed with application exit 0 and no missing frame:

- Downloads, History, Accounts, Settings, Tools, and Trash;
- Cellar and Tasting Room;
- Wide 1456 x 1060 and Compact 960 x 720; and
- onboarding steps 1 through 5, including the inert active-scan presentation at step 4.

The resulting 29 images contain 17 Wide frames at exactly `2912 x 2120` and 12 Compact frames at exactly `1920 x 1440`. `runtime-audit-2026-09-03/S6/SHA256SUMS` verifies every final frame.

All 29 frames were reviewed as contact sheets and at original density where copy, clipping, or action layout required it. Both profiles retain their accepted semantic type, spacing, cards, status treatment, navigation, Flight, and Decanter hierarchy. Compact frames retain the full route header and scroll the single destination body without clipping the shell status surface.

## Findings and corrections

No S6-owned P0, P1, or P2 visual finding remains.

- **P1, corrected:** the first full matrix re-parented route content into a capture host that inherited content sizing, clipping the brand/toolbar and some Compact headers. The final host gives the temporary route or onboarding surface the exact requested extent and top-left alignment before capture.
- **P1, corrected:** an installed Libation window on another macOS Space could make isolated direct-window capture wait despite a valid candidate window. Window discovery now includes off-Space layer-zero windows; direct CoreGraphics capture still targets only the resolved isolated window ID.
- **P2, corrected:** Tools originally paired a legacy book-only `Liberate` count with a confirmation that correctly included PDF-only work. The final card states that the confirmation supplies the exact eligible scope, including titles that need only a PDF, and does not publish a conflicting pre-confirmation number.
- **P1 interaction, corrected:** onboarding could add three titles before Library activation, then lose one when a retained Details-grid selection published during activation. The final ordering activates Library first and applies the explicit Flight gesture at background priority; the exact candidate reports and displays all three newest eligible titles.

The deliberate open vertical field below the short onboarding steps is not hidden content or overflow: the progress/header and bottom navigation remain fixed while each step body stays concise. No destination-specific mockup exists from which to claim pixel parity.

## Intentional reference differences and proof boundary

- The supplied boards describe the primary dashboard hierarchy, not these six destination bodies. S6 extends the accepted profile tokens and shared components instead of inventing pixel parity or shipping reference pixels.
- Demo counts and titles are evidence inputs, not production copy or fixtures shipped to users.
- Capture-only onboarding state does not start a scan or mutate Current Flight.
- These captures prove the named local macOS rendered states only. They do not prove activation, keyboard-only traversal, VoiceOver or another assistive technology, 200% logical scaling, runtime High Contrast/reduced motion, Windows/Linux, installed packaging, notarization, distribution, publication, or release.

final result: passed
