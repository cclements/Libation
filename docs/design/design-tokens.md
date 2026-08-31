# Contemporary Cellar design tokens

Feature XAML consumes semantic resources only. Profile colors live exclusively in
`Source/LibationAvalonia/DesignSystem/Palettes`; a feature view must not reference
a Cellar/Tasting hex value, a Fluent palette property, or a profile-specific
resource name.

Use `DynamicResource` for colors, brushes, density, typography, motion, and other
values that can change while the app is open. Use `StaticResource` only for
immutable templates and vector geometry.

Shared feature views also consume the validated semantic illustration aliases
`illustration.library.empty` and `illustration.decanter.empty` with
`DynamicResource`. `ExperienceManager` resolves those aliases to the appropriate
Cellar or Tasting Room template in the same candidate-resource transaction as
the palette. Profile-specific Overview compositions may use their explicit
illustration IDs; shared routes may not.

## Color and brush roles

Every profile resolves the following `Libation.Color.*` keys and matching
`Libation.Brush.*` aliases:

- `Canvas`, `Sidebar`, `Surface`, `SurfaceRaised`, `SurfaceSunken`, and
  `SurfaceInteractive` establish hierarchy without card nesting.
- `TextPrimary`, `TextSecondary`, `TextTertiary`, and `TextOnAccent` express text
  hierarchy and foreground-on-action contrast.
- `BorderSubtle` and `BorderStrong` separate structure and focusable regions.
- `AccentPrimary`, `AccentSecondary`, `AccentHover`, and `AccentPressed` express
  action states. Cellar's secondary accent is restrained brass; it is not body
  text or decoration on every edge.
- `Focus` and `Selection` are independent from accent so keyboard focus and batch
  selection remain visible.
- `Success`, `Warning`, `Danger`, and `Info` are semantic support colors only;
  status also has literal text and a stable glyph.
- `ProgressTrack`, `ProgressFill`, and `CoverPlaceholder` are operational roles.

`ThemeResourceValidator` treats any missing key or wrong resource type as a
literal profile-load failure. It also validates every brush alias and every
declared shared token; there is no unrelated Fluent-color fallback.

## Profiles

| Profile | Canvas / surface character | Accent | Typography and material intent |
|---|---|---|---|
| Cellar | near-black, black-cherry layered surfaces | burgundy with restrained brass secondary | platform UI sans; serif only for editorial headings; value/border elevation |
| Tasting Room | cream/limestone layered surfaces | burgundy with muted rose secondary | platform UI sans; restrained serif headings; shallow neutral elevation |
| High Contrast | black/white structural surfaces | yellow action, cyan focus/info | system-readable type; no decoration dependency |
| Current Libation interface | semantic preview values are derived from the live Chardonnay/Fluent resources | current system accent | actual global presentation remains owned by Chardonnay |

The current interface is never approximated by a new static palette. Its
semantic preview dictionary is derived at runtime from an explicit allowlist of
live resources, and global application still delegates to `ChardonnayTheme`.

## Spacing, shape, and elevation

| Token family | Declared values | Rule |
|---|---|---|
| `Libation.Space.1…8` | `Double` values 4, 8, 12, 16, 24, 32, 48, 64 | Use for scalar gap properties such as `Spacing`, `RowSpacing`, and `ColumnSpacing`; document a true optical correction. |
| `Libation.Thickness.3…5` | uniform `Thickness` values 12, 16, 24 | Use the typed projection for `Margin` and `Padding`; a dynamic `Double` resource cannot be assigned to `Thickness`. |
| `Libation.Radius.*` | Small 6, Medium 12, Large 18 | Pills are reserved for compact filters/status. |
| `Libation.ControlHeight.*` | Compact 32, Default 40, Prominent 48 | Density must not weaken focus or accessible activation. |
| `Libation.Navigation.Width*` | Expanded 232, Compact 64 | Derived from the reference composition; not a responsive breakpoint. |
| `Libation.Content.MaxReadingWidth` | 920 | Bound long-form copy, not operational grids. |
| `Libation.Shadow.*` | Low and Medium `BoxShadows` | Cellar uses value/border first; Tasting Room uses shallow neutral shadow. |

Runtime density overrides `RowHeight`, `CardPadding`, `ToolbarGap`,
`QueueItemHeight`, and `MetadataOpacity` as one resource dictionary, so feature
views do not branch on `DensityMode`.

## Typography

- `Libation.Font.UI` is Avalonia's `$Default` platform font.
- `Libation.Font.Display` currently uses the documented cross-platform serif
  fallback list. A reviewed bundled editorial font remains an asset option, not
  a requirement for layout correctness.
- “Use system typography” replaces the display family with
  `FontFamily.Default` and is the recovery path for Linux/font-rendering issues.
- The scale is Caption 12, Body Small 13, Body 14, Body Strong 16, Section 20,
  Page 32, and Hero 42. Hero use is limited to spacious overview composition.

## Motion and decoration

- Motion duration tokens are stored as milliseconds: Fast 120, Default 180,
  Deliberate 260. Consumers multiply by `Libation.Motion.Scale`.
- Explicit Reduced motion On resolves the scale to 0; Off resolves it to 1.
- Follow system resolves through `ISystemReducedMotionResolver`. Avalonia 12 has
  no cross-platform reduced-motion API, so unsupported platform adapters report
  unknown and use full motion until Prompt 11 supplies platform evidence.
- `Libation.Decoration.Opacity` resolves Full, Reduced, and Off without changing
  component geometry or command reachability.

## Contrast evidence boundary

Palette values are the plan/reference starting points. High Contrast is a
complete independent palette and does not enter Chardonnay's Light/Dark-only
validator. The resources build and type-validate in production code; automated
contrast execution has not been run because test creation/execution requires
current approval. No contrast launch gate is claimed yet.
