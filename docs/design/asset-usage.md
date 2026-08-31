# Contemporary Cellar asset usage

## Resource inclusion

The production entry point is:

```xml
<ResourceInclude Source="avares://Libation/DesignSystem/Assets/AssetResources.axaml" />
```

It is included after semantic token and palette resources so illustration and
brand templates resolve `Libation.Brush.*` and `Libation.Font.*`. The
contemporary shell remains feature-controlled; the resource keys are safe for
Classic because no Classic styles are replaced.

## Glyphs

Glyphs are 24 × 24 outline masters. Use a semantic brush, a round 1.75 stroke,
and uniform scaling:

```xml
<Path
	Width="20"
	Height="20"
	Data="{StaticResource glyph.library}"
	Fill="Transparent"
	Stroke="{DynamicResource Libation.Brush.TextSecondary}"
	StrokeThickness="1.75"
	StrokeLineCap="Round"
	StrokeLineJoin="Round"
	Stretch="Uniform" />
```

| Render size | Intended use |
|---:|---|
| 16 px | dense row metadata and compact secondary actions |
| 20 px | navigation and standard toolbar actions |
| 24 px | primary controls and ordinary empty-state support |
| 32 px | prominent actions only |

- Do not stretch non-uniformly, recolor with raw profile values, add drop shadows
  for semantic meaning, or substitute a wine metaphor for the literal action.
- Use the outlined master by default. A selected container may supply accent or
  surface treatment; do not silently swap to an unrelated filled glyph.
- Icon-only controls require an accessible name and tooltip. Prefer a visible
  literal label when space permits.
- `glyph.flight` represents an ordered selected batch, not an airplane or grapes.
- `glyph.process` represents the action; `glyph.processing` represents the route
  or ongoing workspace. Keep them distinct.
- Editable/export SVGs live under
  `Source/LibationAvalonia/DesignSystem/Assets/Source/Glyphs/`, one file per
  `glyph.*` contract ID. They use `currentColor`, no embedded text or metadata,
  and the same 24 × 24 geometry/stroke contract as `GlyphAssets.axaml`. Runtime
  consumers should continue using the semantic Avalonia keys rather than loading
  these source exports directly.

## Status marks

Use status geometry with literal status text. Color is redundant, never sole
meaning:

```xml
<StackPanel Orientation="Horizontal" Spacing="6">
	<Path
		Width="16"
		Height="16"
		Data="{StaticResource status.needs-attention}"
		Fill="Transparent"
		Stroke="{DynamicResource Libation.Brush.Warning}"
		StrokeThickness="1.75"
		StrokeLineCap="Round"
		StrokeLineJoin="Round"
		Stretch="Uniform" />
	<TextBlock Text="Needs attention" />
</StackPanel>
```

- Preserve exact product state; do not map Downloaded to Completed or Pending to
  Processing merely because their colors are similar.
- Cellar wax and Tasting Room ring treatments may wrap the shared mark only when
  decoration is enabled. They do not replace the geometry, text, or accessible
  name.
- Progress remains a real progress control. `status.downloading` and
  `status.processing` are state identifiers, not progress meters.

## Brand

- Render `brand.mark.one-color`, `brand.mark.light`, and `brand.mark.dark` with a
  1.75 round outline at runtime.
- The light/dark keys intentionally share original geometry. Semantic brushes
  provide contrast without maintaining divergent silhouettes.
- The horizontal wordmark resources are `ControlTemplate` values intended for a
  `ContentControl`:

```xml
<ContentControl Template="{StaticResource brand.wordmark.horizontal.light}" />
```

- Do not use the legacy wine-glass SVG/ICO as a source for redraws.
- The runtime lockups are the production wordmarks: a shared vector mark plus live
  accessible text. No current shipping consumer requires an outlined/static
  wordmark export, and creating one would duplicate font outlines and introduce
  font-license, fallback, scaling, and accessibility drift.
- Do not export package icons from a screenshot of the runtime template. A later
  asset-export change must use the code-native master, perform optical review at
  platform sizes, and update every packaging target as one controlled change.

## Functional illustrations

Illustrations are optional visual support, never the state container. Host them
with a `ContentControl` and keep live copy and commands outside the template:

```xml
<ContentControl
	Width="192"
	Height="144"
	IsVisible="{Binding DecorationsEnabled}"
	Template="{StaticResource illustration.shared.no-search-results}" />
```

- The natural aspect ratio is 4:3 from a 96 × 72 internal canvas.
- Keep functional illustrations between 96 × 72 and 256 × 192 logical pixels.
  A layout may use a smaller mark when the accompanying message remains clear.
- Do not place copy or controls over an illustration.
- Decoration Off hides the illustration host while leaving spacing, title,
  explanation, primary action, focus order, and accessible name complete.
- High Contrast uses the same template with High Contrast semantic brushes.
- Cellar and Tasting Room variants may differ in composition but never in the
  literal meaning or available command.

## Deferred decoration

No runtime resource is published for `illustration.cellar.bottle-rack-motif`,
`illustration.tasting-room.still-life`, or `texture.cellar.grain-seamless`.
Consumers must not reserve required layout space for them or probe for them as a
feature dependency. If later reinstated, they require original sources,
provenance, Decoration Off behavior, and review for excess alcohol-brand cues.

## Package exports

`Source/LibationAvalonia/DesignSystem/Assets/Source/brand-app-icon.svg` is the
editable, text-free, original source master. It has been exported to:

- Windows ICO frames at 16, 20, 24, 32, 40, 48, 64, 96, 128, and 256 px,
  including Avalonia, WinForms, and installer copies;
- macOS ICNS representations from 16 through 1024 px and the bundle resource;
- the Linux scalable application SVG consumed by Debian/RPM packaging;
- all known repository consumers of those three platform assets.

The five Windows ICO copies are byte-identical and contain the required ten
frames; the ICNS unpacks to the standard ten representations; the Linux SVG is
byte-identical to its source master. These checks establish file-level export
integrity only. No installed-artifact or platform-package claim is earned until
the outputs are reviewed in their actual taskbar, Dock, launcher, and installer
contexts.

## Contact-sheet evidence

Open `docs/design/asset-contact-sheet.svg` directly in a browser or vector viewer.
It is documentation only and must not be added to `AssetResources.axaml`. It covers
all `glyph.*` and `status.*` keys at 16, 20, 24, and 32 logical pixels under Cellar,
Tasting Room, High Contrast, and Decoration Off evidence palettes. Decoration Off
deliberately leaves every functional mark visible; it suppresses only optional
illustration hosts and decorative treatment.

The sheet uses the production 24 × 24 paths, a non-scaling 1.75 px round stroke,
and semantic palette values copied into the standalone evidence file. Runtime code
must continue to consume dynamic semantic brushes instead of those documented
literal values.
