# Contemporary Cellar release review

Decision: **NO-GO for rollout; keep the source integration experimental and default-off.**

S9 is complete at the source, automated, headless-render, and non-release package-artifact tiers. Exact source `d78737f8` passed fork-only validation run `33826389731` across three Windows, two macOS, and four Linux jobs. That run produced nine platform packages plus Windows and Linux capture artifacts. It did not install or launch those packages, exercise a manual screen reader, sign or notarize the macOS bundles, distribute a beta, publish a release, or authorize rollout.

## Rollout controls

| Stage | Current state | Advancement evidence |
|---|---|---|
| experimental opt-in | Implemented by `UseContemporaryShell=false` plus explicit Settings selection | S9 complete at its admitted tiers; retain while installed-package and accessibility evidence is absent. |
| stable opt-in | Not authorized | Owner-approved installed-package launches, manual accessibility evidence, and beta criteria. |
| one-time existing-user offer | Not enabled | Stable opt-in evidence, verified Skip/current-interface paths, and explicit owner approval. |
| default for new installs | Not enabled | Supported-platform beta evidence and explicit owner approval. |
| remove current interface | Out of scope | Separate major-release decision. |

The escape hatch remains immediate: turning off the contemporary-shell setting makes `MainWindow` restore its original content and minimum-size contract without migrating the library or queue.

## Gate matrix

| Gate | Current evidence | Result |
|---|---|---|
| one library, one Flight, one queue, routed existing commands | source review, focused contracts, and full Release automation | pass at source/automation tier |
| default-disabled flag and current-interface path | persisted default plus live host opt-out contract | pass at automation tier |
| invalid settings and downgrade preservation | invalid-profile repair and legacy-writer preservation contracts | pass at automation tier |
| compiled resources and XAML | all nine test projects, publish steps, and bundles passed in the nine-job CI matrix | pass at CI tier |
| dashboard, Flight, filtering, Gallery projection, and large deterministic fixtures | S8 focused contracts and 1k/10k/50k fixture integrity evidence | pass at the recorded source/automation tier |
| keyboard, focus, contrast, reduced motion, and 200% scale | recorded macOS interaction evidence plus automated accessibility-tree peers | pass only for the recorded paths; manual screen-reader proof remains open |
| Windows and Linux profile rendering | Cellar and Tasting Room headless captures from Windows x64 and Debian x64, visually inspected at 960 x 720 | pass at headless-render tier; not an installed-app launch |
| Windows ZIPs, macOS DMGs, Linux DEB/RPM | three Windows, two macOS, and four Linux non-release artifacts with GitHub SHA-256 digests | pass at package-artifact tier; installation, signing, and launch remain open |
| no P0/P1 defects | no installed-package, manual assistive-technology, or beta evidence | cannot admit for rollout |

## Known limitations for an experimental build

- Overview storage and update freshness use only data already exposed by current services; the shell does not invent a new storage or updater source.
- Decanter recovery retries only failed download/decrypt rows whose owner can reconstruct the request and routes failed rows to the retained Queue Log. Other operation types do not invent retry or reveal recipes.
- Follow-system reduced motion falls back to full motion where no platform resolver exists; explicit Reduce remains available.
- System typography remains the recovery path for platform-specific text rendering.
- Runtime wordmarks use vector mark geometry and live platform text; optional outlined wordmark exports have no shipping consumer.

## Release-note draft

> Experimental: Contemporary Cellar adds optional Cellar and Tasting Room layouts, a shared Overview, a modern Library with Details and Gallery modes, Current Flight batch selection, and a Decanter view of the existing processing queue. Enable it explicitly in Settings. Your library and processing behavior remain owned by the same Libation services, and you can return to the current interface at any time. CI now builds non-release artifacts for supported platforms, but this preview has not completed installed-package, manual accessibility, signing/notarization, beta-distribution, or rollout gates.

The S9 source commits and fork-only CI vehicle were authorized. The temporary fork-only draft pull request is evidence infrastructure, not upstream contact or acceptance. No upstream write, maintainer contact, package publication, beta distribution, signing/notarization, default change, or release is authorized by this workstream.
