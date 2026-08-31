# Contemporary Cellar release review

Decision: **NO-GO for rollout; source integration remains experimental and
default-off.**

This decision is evidence-based, not a statement that the implementation is
known to be defective. The source and compiled XAML build on the current macOS
host, and the authorized 23-case settings/persistence/scrubber packet passes.
The plan's interaction, accessibility, migration, performance, package, and
supported-platform launch evidence has not been reproduced.

## Rollout controls

| Stage | Current state | Advancement evidence |
|---|---|---|
| experimental opt-in | implemented by `UseContemporaryShell=false` plus explicit Settings selection | complete source fan-in and named macOS interaction evidence |
| stable opt-in | not authorized | approved defect disposition; keyboard/accessibility/rollback evidence; supported-platform beta packages |
| one-time existing-user offer | not enabled | owner approval after stable opt-in evidence; Skip and Current-interface paths verified |
| default for new installs | not enabled | owner approval after beta evidence and supported-platform launch gates |
| remove current interface | out of scope | later major-release decision based on usage and issue evidence |

The escape hatch is immediate: turn off the contemporary-shell setting and
`MainWindow` restores its original content and minimum-size contract without a
library or queue migration.

## Gate matrix

| Gate | Current evidence | Result |
|---|---|---|
| one library, one Flight, one queue, routed existing commands | direct source review and Release compilation | source-pass |
| default-disabled flag and current-interface path | direct persistence/host review plus focused settings tests | unit-pass for defaults and round trip; runtime rollback unverified |
| compiled resources/XAML | macOS Release build, 0 warnings / 0 errors | pass for this target only |
| batched settings final state and copied-diagnostic privacy | focused dictionary, experience-settings, and scrubber cases | pass for selected contracts only; crash/concurrent-observer atomicity remains unverified |
| asset source/provenance | vector dictionaries, SVG master, manifest, platform input files | source-pass; installed packages unverified |
| no raw reference-board pixels ship | repository asset review | source-pass |
| profile/dialog/active-processing stability | no isolated interaction run | unverified |
| keyboard, focus, screen reader, contrast, 200% scale | no runtime accessibility run | unverified |
| Gallery virtualization/cache/large-library performance | source review only; deterministic fixture absent | unverified |
| upgrade, corrupt-settings recovery, downgrade, crash restart | source review only | unverified |
| Windows, macOS packages, Linux DEB/RPM and GNOME/KDE | no package matrix run | unverified |
| no P0/P1 defects | no complete runtime/platform evidence | cannot admit |

## Known limitations for an experimental build

- Overview storage/update freshness uses only data already exposed by current
  services; it does not invent a new storage or updater source.
- Decanter recovery exposes retry for failed download/decrypt rows whose owner
  can reconstruct the request, and routes failed rows directly to the retained
  Queue Log. Other operation types do not invent retry/reveal recipes.
- Follow-system reduced motion falls back to full motion where no platform
  resolver exists; explicit Reduce remains available.
- System typography is the recovery path until Ubuntu rendering is reproduced.
- Runtime wordmarks use vector mark geometry and live platform text; optional
  outlined wordmark exports have no current shipping consumer.

## Release-note draft

> Experimental: Contemporary Cellar adds optional Cellar and Tasting Room
> layouts, a shared Overview, a modern Library with Details and Gallery modes,
> Current Flight batch selection, and a Decanter view of the existing processing
> queue. Enable it explicitly in Settings. Your library and processing behavior
> remain owned by the same Libation services, and you can return to the current
> interface at any time. This preview has not completed the supported-platform
> accessibility and packaging matrix; report rendering, keyboard, discovery, or
> workflow differences with the beta template.

Local commits and delivery to the `cclements` fork branch are authorized. No
pull request, upstream write, maintainer contact, package publication, beta
distribution, or default-rollout action is authorized by this source workstream.
