# Contemporary Cellar program baseline
This file binds the implementation pack to the live repository before production
UI changes. The UI intake was measured at
`094e207c0b245f36592ce31000f693674b886057`; the final source delivery is
rebased onto `3e7191adc7f41f1dec252b95e505b3f318be3b34` from `origin/master` on
2026-08-30.

## Repository and execution plan

- Integration branch: `codex/contemporary-cellar`, created from the measured UI
  intake and rebased onto the final upstream delivery baseline.
- Existing unrelated worktrees and staged changes are out of scope and remain
  untouched.
- Work proceeds serially on the integration branch so each dependency is
  reconciled before its consumer changes.
- The 29 scopes in the pack remain logical review boundaries. Local commits and
  pushes to the `cclements` GitHub fork are authorized. Pull requests,
  maintainer contact, writes to upstream, publication, and rollout are not.
- If review branches are later requested, their names will use
  `codex/contemporary-cellar-<scope>` and start from the last accepted dependency,
  not from an unrelated staged worktree.

## Dependency DAG

```text
01 Baseline / ADR / visual intake
  └─ 02 Semantic tokens / persistence / profile resolution
      ├─ 03 Production assets (sole asset owner)
      └─ 04 Shared controls
          └─ 05 Route model / shell / disabled flag / legacy embedding
              ├─ 06 Overview profiles
              ├─ 07 Library Details / Gallery / book details
              │   └─ 08 One Flight service / both profile presentations
              ├─ 09 Decanter over the existing queue
              └─ 10 Secondary screens / migration / onboarding
                  └─ 11 Accessibility / visual / performance / platform evidence
                      └─ 12 Release review / rollback / rollout decision
```

Prompt 03 may prepare assets after Prompt 02 stabilizes asset IDs and token
semantics. It may not independently rename resources consumed by Prompt 04.
Continuous accessibility review begins with shared controls, but Prompt 11 owns
the final cross-platform and visual-regression evidence.

## Logical review sequence

The pack's PR map remains authoritative. For implementation, its scopes group as:

| Dependency tranche | Pack PR scopes | Prompt owners | Merge condition |
|---|---:|---|---|
| Architecture | 1 | 01 | ADR, command parity, honest baseline, visual gate |
| Profile substrate | 2–4 | 02 | no visual regression while flag is off; profile resources complete |
| Shared UI and shell | 5–8 | 04, 05 | one state graph; current interface rollback |
| Overview | 9–11 | 06 | shared aggregation, both compositions |
| Library | 12–14 | 07 | Details parity; virtualized Gallery; details focus |
| Batch and processing | 15–19 | 08, 09 | one Flight and one queue source |
| Secondary surfaces | 20–23 | 10 | command parity, migration, literal destructive copy |
| Assets/motion | 24–25 | 03, 04, 11 | provenance, fallback, reduced motion/decoration |
| Evidence and rollout | 26–29 | 11, 12 | supported-platform, accessibility, rollback, owner rollout decision |

## Contracts that stabilize first

1. Persisted experience values are UI-agnostic and backward compatible.
2. Semantic resource keys are the only profile-color/metric API for feature
   views.
3. `ExperienceManager` is the sole effective-profile resolver and resource
   switcher; preview scopes do not mutate global application state.
4. One `MainVM`, one `ProductsDisplayViewModel`, and one
   `ProcessQueueViewModel` remain live.
5. One route model and one command-adapter layer own shell navigation and command
   reachability.
6. One stable-ID Flight service owns batch selection.
7. Prompt 03 alone creates/modifies production brand, glyph, illustration, and
   app-icon assets; other workstreams consume declared IDs and fallbacks.
8. The current DataGrid remains Details mode and retains customization behavior.
9. Reference PNGs are evidence only and never ship as rasterized interface
   boards.

## Conflicts bound against current source

- Numeric macOS route gestures are unavailable because quick filters own them.
- “Classic” is reserved by a shipped WinForms artifact; the Avalonia fallback is
  `CurrentAvalonia` internally.
- High contrast requires its own semantic palette.
- The existing live theme preview is extended rather than replaced.
- Gallery and Flight require an adapter because DataGrid selection and the source
  list are not public VM contracts.
- Decanter must account for the newly concurrent queue and must not duplicate its
  VM or legacy control.
- Positional native-menu mutation is too fragile for shell reorganization.
- Large-library performance gates cannot pass until a deterministic fixture and
  measurement contract are authorized.

## First executable tranche

The first production tranche has no visible behavior change:

1. add backward-compatible persistence with `UseContemporaryShell=false`;
2. add semantic tokens and complete Cellar, Tasting Room, and high-contrast
   palettes;
3. add `ExperienceManager` and a legacy Chardonnay adapter;
4. extend the existing preview with scoped profile selection;
5. keep `MainWindow`, its `MainVM`, library VM, and queue VM structurally
   unchanged.

Only after that tranche builds and its resource contract is reviewed may the
disabled shell host the existing state graph.
