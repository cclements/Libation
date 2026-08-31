# Contemporary Cellar
Contemporary Cellar is an opt-in Avalonia presentation for Libation. It gives
the same library, processing queue, commands, and persisted data two deliberate
compositions: Cellar and Tasting Room. It does not create a second application
state graph, processing engine, or library index.

## Product intent

- Make the library and current work legible before exposing advanced controls.
- Preserve every established Details-grid and native-menu workflow.
- Give batch intent a stable, reversible home in Current Flight.
- Present the existing queue as The Decanter without changing queue semantics.
- Keep the shipped Avalonia interface available as the immediate rollback path.

## Profiles

| Profile | Composition | Character | Shared behavior |
|---|---|---|---|
| Cellar | library-first overview with a persistent Flight pane when space permits | dark, layered, restrained burgundy and brass | routes, commands, selection, queue, settings, and data |
| Tasting Room | spacious overview with Flight as a card or drawer | light limestone surfaces with a burgundy accent | routes, commands, selection, queue, settings, and data |
| High Contrast | accessible, low-decoration composition | black/white structure with explicit focus and status colors | the same contemporary state and commands |
| Current Libation interface | the existing `MainWindow` content | current Chardonnay/Fluent appearance | the rollback path; no data migration is required |

Changing profile changes resources and composition only. Current Flight,
navigation, the authoritative `ProductsDisplayViewModel`, and the authoritative
`ProcessQueueViewModel` remain alive across a profile switch.

## Language and terminology

The wine vocabulary is secondary and never required to understand an action.

| Term | Literal meaning |
|---|---|
| Current Flight | selected titles for a later batch operation |
| The Decanter | the existing processing queue and its current outcomes |
| Download Pending | catalogued, but without a local open-format audiobook copy |
| Remove from Current Flight | remove from the batch selection; it does not delete a book |
| Trash | the established protected restore/permanent-delete workflow |

Buttons, warnings, confirmations, errors, and recovery instructions use literal
verbs. Status always combines a glyph with readable text; color is never the
only state carrier.

## Accessibility intent

- Native window chrome, menus, and established shortcuts remain available.
- Semantic brushes include independent focus, selection, status, and text roles.
- High Contrast, system typography, Compact density, reduced motion, and
  Decoration Off are first-class settings rather than special screenshots.
- Responsive layouts preserve the same commands at the minimum supported shell
  size; contextual panes become drawers instead of disappearing.
- Loading, empty, no-results, warning, blocking, active, completed, failed, and
  cancelled states use explicit text and accessible names.

The resources and XAML compile, but keyboard, screen-reader, 200% scaling,
contrast, and supported-platform launch gates remain evidence tasks. They are
not inferred from source inspection or a macOS build.

## Visual sources and evidence boundary

The four approved reference boards and their crops live in
`libation-contemporary-cellar-complete-agent-pack` outside this repository.
Their hierarchy, proportions, palette, glyph vocabulary, and signature surfaces
inform production code. No generated board pixels, fake cover art, or generated
lettering ship in the application.

Current production assets are code-native vectors with recorded provenance in
`docs/design/asset-manifest.md`. Runtime screenshots have not been admitted
because this task does not yet have a safe isolated app-launch configuration or
the required cross-platform environments. See
`docs/design/contemporary-cellar-release-review.md` for the exact evidence
boundary.
