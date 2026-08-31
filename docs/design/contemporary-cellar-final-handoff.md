# Handoff: Contemporary Cellar consolidated source integration

## Outcome
- Completed: Prompts 01–10 are integrated at source level on the default-off
  Contemporary Cellar graph; Prompt 12 source review records a rollout
  **NO-GO**.
- Partially completed: Prompt 11 has a documented evidence matrix, and the final
  current-source macOS Release compilation succeeded with 0 warnings and 0
  errors. Neither is an admitted runtime, test, package, or supported-platform
  result.
- Not completed: automated tests, isolated app interaction, major-screen
  comparisons in both profiles, accessibility, 10k/50k performance,
  migration/rollback interaction, installed packages, Windows/Linux coverage,
  beta evidence, upstream-project acceptance, and rollout authorization. An
  upstream pull request and maintainer contact are explicitly outside this
  delivery.

## Files changed
- `Source/LibationFileManager/ContemporaryExperienceSettings.cs` and related
  persistence files: backward-compatible contemporary preferences and atomic
  settings persistence.
- `Source/LibationAvalonia/DesignSystem/`: semantic profiles, shared controls,
  vector asset contract, source exports, and profile-scoped preview resources.
- `Source/LibationAvalonia/Shell/`: one route model, responsive shell, current
  owner adapters, and the feature-controlled host.
- `Source/LibationAvalonia/Features/`: Overview, Library, Current Flight,
  Processing/Decanter, typed command outcomes, shared user-facing errors,
  secondary destinations, and onboarding presentations.
- `Source/LibationUiBase/Diagnostics/` and process-queue seams: scrubbed copied
  diagnostics and queue-log presentation with one correlation reference per job.
- `Source/LibationAvalonia/Properties/`: English route and command resources
  used by the shell and typed route catalog.
- `Source/LibationAvalonia/App.axaml`, `App.axaml.cs`, `Views/`,
  `ViewModels/`, `Controls/`, and `Dialogs/`: fan-in with the existing
  `MainVM`, Details grid, process queue, menus, settings, and dialogs.
- `Scripts/Windows/libation.ico`, Avalonia/WinForms icon inputs,
  `Source/LoadByOS/MacOSConfigApp/libation.icns`, and
  `Source/LoadByOS/LinuxConfigApp/libation_glass.svg`: original
  open-book/carafe platform icon exports; file-level only.
- `docs/adr/`, `docs/design/`, and `docs/development/`: architecture,
  preservation contracts, source/evidence ledgers, asset provenance, release
  review, and this consolidated handoff.
- Current delivery state: the source is committed on
  `codex/contemporary-cellar`, rebased onto upstream
  `3e7191adc7f41f1dec252b95e505b3f318be3b34`, and prepared for the same-named
  branch on the `cclements` GitHub fork. No pull request or upstream write is
  authorized.

## Architecture decisions
- Decision: keep one existing library, one shell-scoped stable-ID Flight
  service, one existing process queue, and one Avalonia route service.
- Rationale: new presentation must not duplicate domain state, selection,
  execution, or command ownership.
- Compatibility impact: the contemporary graph adapts `MainVM`,
  `ProductsDisplayViewModel`, and `ProcessQueueViewModel`; the existing Details
  grid, queue controls/log, native menus, dialogs, and gestures remain
  authoritative.
- Decision: resolve Cellar, Tasting Room, Follow System, High Contrast, and
  preview resources through one `ExperienceManager` and semantic
  `Libation.*` keys.
- Rationale: both first-class profiles need shared behavior with atomic,
  reversible presentation changes.
- Compatibility impact: `UseContemporaryShell` defaults to `false`; feature
  views do not replace current-interface resources or ship reference-board
  pixels.
- Decision: owner operations return typed Completed, Cancelled, or No-change
  outcomes; persistent errors and queue failures carry scrubbed technical detail
  joined to established logs by a correlation ID.
- Rationale: presentation must never infer completion from prose or expose raw
  exception/credential/path text as its primary copy.

## Build and tests
- Build command: `/Users/chris/.dotnet/dotnet build
  Source/LibationAvalonia/LibationAvalonia.csproj --configuration Release
  --no-restore --disable-build-servers -m:1 -v:minimal`.
- Build result: the final post-rebase pass succeeded on macOS in 19.77 seconds
  with 0 warnings and 0 errors. Earlier compile passes exposed and then closed
  the missing Avalonia clipboard extension imports and XAML-required public
  parameterless Locate Audiobooks constructor.
- Test command: none. Tests were deliberately not run because execution requires
  current exact approval; there is no test pass.
- Uncovered risk: persistence repair/restart, route and shortcut behavior,
  Flight synchronization/preflight, live queue actions, onboarding, privacy,
  focus/screen-reader behavior, performance, migration/rollback interaction,
  and supported-platform packaging.

## Visual verification
- Profile/platform/scale: both profile references and focused crops were
  verified at source resolution; the vector contact sheet was reviewed across
  Cellar, Tasting Room, High Contrast, Decoration Off, and 16/20/24/32 logical
  sizes. No in-app supported-platform/scale matrix was run.
- Evidence path: `docs/design/visual-evidence-map.md`,
  `docs/design/asset-contact-sheet.svg`,
  `docs/design/asset-manifest.md`, and the workstream-specific design records.
- Known mismatch: major-screen runtime captures, responsive states, typography,
  focus, assistive output, DPI rasterization, native menus, and installed icons
  remain unverified; source composition is not visual acceptance.

## Asset dependencies
- Consumed asset IDs: the shipping source graph consumes the stable
  `brand.*`, `glyph.*`, `status.*`, and functional `illustration.*` resources
  declared by `AssetResources.axaml`; platform inputs derive from the original
  `brand-app-icon.svg` master.
- Missing asset IDs: none required by the current source contract.
  `illustration.cellar.bottle-rack-motif`,
  `illustration.tasting-room.still-life`, and
  `texture.cellar.grain-seamless` are explicitly deferred decoration with no
  layout or behavior dependency. Runtime/DPI and installed-package evidence is
  still missing.

## Migration and rollback
- Migration behavior: contemporary settings are backward-compatible and
  default to the current interface. Invalid contemporary values repair their
  entry and disable the new shell in one replacement. Persisted Flight ID JSON
  arrays deserialize through the shared configuration converter. Onboarding
  commits profile and shell activation through one atomic settings-file
  replacement with `UseContemporaryShell` published last.
- Feature flag / rollback path: `Configuration.UseContemporaryShell=false`
  immediately restores the current Avalonia content and minimum-size contract;
  no library, selection, queue, or media migration is required.

## Follow-up work
- Blocking: define an isolated no-account configuration; obtain exact approval
  for the named deterministic test and interaction packets; provide the
  performance fixture and supported Windows/macOS/Linux environments; execute
  the visual/accessibility/package/rollback matrices; dispose resulting
  defects; then obtain explicit owner rollout authority.
- Non-blocking: richer domain-owned Downloads, History, and privacy-safe account
  adapters; owner disposition of account-removal and Trash confirmation gaps;
  and any future decorative assets with provenance and Decoration Off review.

## Ready for next workstream
- No — not for merge, beta admission, or rollout.
- Required prerequisite if No: authorize and supply the Prompt 11 evidence
  environment and packet; after its gates pass, the owner can perform the
  Prompt 12 release decision.
