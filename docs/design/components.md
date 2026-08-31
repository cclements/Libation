# Contemporary Avalonia component library

This library is the presentation-only component vocabulary for the contemporary
Libation shell. It accepts display data, content slots, templates, selection,
progress values, and `ICommand` instances from a feature host. It does not own
library data, account state, processing state, routing, queue lifetime, toast
lifetime, file operations, or settings persistence.

## Integration

The consuming contemporary surface includes the shared style entry point:

```xml
<StyleInclude Source="avares://Libation/DesignSystem/Styles/All.axaml" />
```

`All.axaml` includes the production asset dictionary and the button, input,
search/filter, menu, progress, card, list, and DataGrid style sheets. Semantic
tokens and a profile palette must already be available through the active
`ExperienceManager` resource scope. Current-interface controls are unaffected:
every shared native-control style is class-scoped.

Use `DynamicResource` for profile tokens so live experience, density,
decoration, typography, and reduced-motion changes flow through existing
controls. Glyph and status resources are semantic IDs; their stroke/fill always
comes from semantic brushes.

## Production components

| Component | Host-supplied surface | Included semantic states |
|---|---|---|
| `LibationNavigationRail` | items, item template, selected item, expanded/compact state, brand and footer slots | expanded, compact, selected native list item, badge/attention content through the item template, native hover/pressed/focus/disabled |
| `PageHeader` | eyebrow, title, supporting text, primary command, secondary action collection/template, status and hero-art slots | actions omitted/present, status omitted/present, decoration opacity |
| `MetricCard` | icon geometry, value, label, delta, literal status, severity, optional command | neutral, info, success, warning, danger |
| `BookCard` | optional cover, title/author/narrator/duration, status, selection, progress, open and context commands | cover/placeholder, default, selected, progress, disabled, all canonical statuses |
| `BookRow` | optional cover, title/supporting text/metadata, status, selection, progress, open and context commands | cover/placeholder, default, selected, progress, all canonical statuses |
| `StatusBadge` | canonical status enum, optional literal-text and accessible-name overrides | Download pending, Downloading, Downloaded, Processing, Completed, Failed, Cancelled, Unavailable, Needs attention |
| `FlightTray` | ordered items/template, count, warning, output-profile copy, Process and Clear commands | populated/empty host data, warning/no warning, enabled/disabled commands |
| `DecanterSummary` | aggregate and active text, progress, details slot, expanded state, supported Pause/Cancel commands | collapsed, expanded, progress, supported/unsupported actions |
| `QueueItem` | book/stage/message, canonical status, progress, expanded error details, Retry/Reveal/Cancel commands | processing, completed, failed/error expanded, actions omitted when unsupported |
| `DropZone` | title, hint, accepted-type copy, drag-over state, permission/error copy, Browse command | default, drag over, permission/error; keyboard Browse remains available |
| `AttentionBanner` | severity, title/message, remediation and dismiss commands | neutral, info, success, warning, danger |
| `EmptyState` | optional functional `IControlTemplate`, title/explanation, primary and secondary commands | neutral glyph fallback, functional illustration, decoration full/reduced/off |
| `ToastHost` | presentation-owned message collection or custom item template | undo, completion, non-blocking warning, copied, transient failure |
| `ThemePreviewCard` | profile copy, action, literal badge, progress, selected-row sample | real surface, button, badge, progress control, selected row |

All component properties are Avalonia styled properties so feature views can
bind them without replacing their `DataContext`. Collection components accept
`IEnumerable` and optional `IDataTemplate`; they make no assumptions about a
domain view-model type. Commands and parameters remain host-owned.

## Native control style classes

- Buttons: `contemporary`, with optional `primary`, `danger`, `quiet`, and
  `compact` classes.
- Text input and selectors: `contemporary`; search boxes also use `search`.
- Filter chips: `filter`, plus `selected` when active.
- Menus and menu items: `contemporary`.
- Progress: `contemporary`, plus `prominent` for aggregate progress.
- Card borders: `card`, with optional `raised` and `selected`.
- Lists and DataGrids: `contemporary`.

The style sheets leave native control peers, keyboard handling, selection,
focus, command enablement, text editing, menu behavior, and progress semantics
intact. Pointer-over, pressed, selected, focus-visible, and disabled states are
the platform states of those controls rather than painted imitations.

## Asset contract and fallbacks

`StatusBadge` maps its nine exact states to `status.*` IDs and always renders
literal text plus an accessible name; color is redundant. Navigation, Flight,
output profile, processing, library, add-books, and other functional marks use
the corresponding `glyph.*` IDs. The gallery hero uses the shared
`brand.mark.one-color` geometry.

Functional illustrations are `ControlTemplate` resources and can be passed to
`EmptyState.IllustrationTemplate`, for example:

```xml
<components:EmptyState
    IllustrationTemplate="{DynamicResource illustration.cellar.empty-library}"
    Title="Your cellar is ready"
    Explanation="Scan your library or add local files." />
```

The host selects the illustration whose semantic meaning and profile are
correct. When no illustration is supplied, `EmptyState` uses the neutral
library glyph. Missing book covers use the neutral cover surface and library
glyph. `StatusBadge` retains an inline neutral outline if its asset dictionary
is unavailable, so the XAML can still load and the literal state remains
available. Decorative hosts are removed from the accessibility tree and obey
`Libation.Decoration.Opacity`; required copy and commands never depend on them.

## Accessibility and behavior boundaries

- Status is expressed by literal text, icon shape, and accessible name, never
  color alone.
- Buttons, ListBox, Expander, TextBox, ComboBox, Menu, ProgressBar, and DataGrid
  remain real Avalonia controls with native keyboard, focus, selection, and
  automation behavior.
- Icon-only/decorative paths are excluded from the accessibility tree. Visible
  action labels and automation names identify operations.
- Banners, queue progress, and toasts use polite live regions. Critical or
  blocking problems belong in `AttentionBanner` or the feature's modal flow,
  not in `ToastHost`.
- `DropZone` exposes a real Browse button for keyboard and assistive-technology
  users. The feature host owns drag event validation and file handling and binds
  the resulting drag/error state.
- `ToastHost` only presents messages. The host owns admission, ordering,
  dismissal, timing, and action semantics; critical errors must not disappear
  on a timer.

## Developer gallery

`ComponentGallery` is an unshipped developer surface composed entirely from the
production controls and shared native styles. Its toolbar selects Cellar,
Tasting Room, or High Contrast; Comfortable or Compact density; Full, Reduced,
or Off decoration; motion preference; and system typography. Each selection
uses `ExperienceManager.CreatePreviewScope` and assigns resources only to the
gallery's local `ThemeVariantScope`. It never writes `Configuration` or changes
application/domain state.

The gallery renders every component and the semantic state families listed in
the table, including all nine status badges, every banner severity, selected
and disabled book surfaces, progress, collapsed/expanded Decanter, processing /
completed / failed queue rows, normal / drag-over / permission-error drop zones,
all five toast kinds, and the empty state. Native hover, pressed, focus-visible,
menu, selection, and keyboard states are exercised directly by interacting with
the real controls. Gallery commands deliberately do nothing.

The selector cross-product is the review surface for all three profiles, both
density modes, and all three decoration levels; no preview selection is a
persisted product preference.
