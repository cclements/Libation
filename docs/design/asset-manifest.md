# Contemporary Cellar asset manifest

This manifest records the production-safe code-native asset surface created for
the Contemporary Cellar experience. All paths are relative to the repository
root. The source PNG boards and crops remain visual evidence only and are not
runtime inputs.

## Provenance and license

- Source: original geometry and templates authored in
  `Source/LibationAvalonia/DesignSystem/Assets/` on 2026-08-30.
- License: GPL-3.0, matching the repository license.
- External source material: none. The assets were drawn from functional meaning
  and the written visual contract, not traced from the generated boards.
- Embedded text: none in geometry or illustration masters. The runtime wordmark
  template uses live text rendered by the platform UI font.
- Intended profiles: shared unless an ID explicitly names Cellar or Tasting
  Room. High Contrast consumes the same geometry with semantic brushes.

## Source dictionaries

| Source | Contents | Runtime resource type |
|---|---|---|
| `Source/LibationAvalonia/DesignSystem/Assets/BrandAssets.axaml` | app-icon/mark master and horizontal runtime lockups | `StreamGeometry`, `ControlTemplate` |
| `Source/LibationAvalonia/DesignSystem/Assets/GlyphAssets.axaml` | complete 24 × 24 core glyph vocabulary | `StreamGeometry` |
| `Source/LibationAvalonia/DesignSystem/Assets/Source/Glyphs/*.svg` | one optimized, editable 24 × 24 SVG export per core glyph ID | SVG source/export |
| `Source/LibationAvalonia/DesignSystem/Assets/StatusAssets.axaml` | complete 24 × 24 semantic status vocabulary | `StreamGeometry` |
| `Source/LibationAvalonia/DesignSystem/Assets/IllustrationAssets.axaml` | nine functional, text-free illustration masters | `ControlTemplate` |
| `Source/LibationAvalonia/DesignSystem/Assets/AssetResources.axaml` | single include point for all production assets | `ResourceDictionary` |
| `Source/LibationAvalonia/DesignSystem/Assets/Source/brand-app-icon.svg` | editable application-icon master | SVG |

## Brand assets

| Asset ID | Source/key | Profiles | Export target | Status |
|---|---|---|---|---|
| `brand.app-icon` | `BrandAssets.axaml`; `DesignSystem/Assets/Source/brand-app-icon.svg` | shared | Windows 16/20/24/32/40/48/64/96/128/256; macOS ICNS 16–1024; Linux scalable SVG | Exported to every known Avalonia, WinForms, installer, macOS bundle, and Linux package consumer. File/frame validation complete; installed-artifact review remains open. |
| `brand.mark.one-color` | `BrandAssets.axaml` | shared | SVG and Avalonia geometry | Ready. |
| `brand.mark.light` | `BrandAssets.axaml` | light surfaces | SVG and Avalonia geometry | Ready as a semantic geometry master. |
| `brand.mark.dark` | `BrandAssets.axaml` | dark surfaces | SVG and Avalonia geometry | Ready as a semantic geometry master. |
| `brand.wordmark.horizontal.light` | `BrandAssets.axaml` | light surfaces | Avalonia runtime template | Production-ready runtime lockup: shared vector mark plus live accessible text. No shipping consumer requires an outlined export. |
| `brand.wordmark.horizontal.dark` | `BrandAssets.axaml` | dark surfaces | Avalonia runtime template | Production-ready runtime lockup: shared vector mark plus live accessible text. No shipping consumer requires an outlined export. |

The mark combines open-page curves with a central carafe outline. It is original
and does not reuse the legacy Flaticon-derived wine-glass geometry.

Outlined wordmark SVGs are intentionally not exported. Every shipping consumer
uses the runtime `ControlTemplate`, whose vector mark and live text scale without
duplicating a font outline, preserve accessible text, and follow platform font
fallback. A future static/package consumer would create a new export requirement
with its own font-license and optical-review evidence; none exists today.

## Core glyphs

Every key below is an original `StreamGeometry` normalized to a 24 × 24
coordinate system in `GlyphAssets.axaml`, with a matching optimized individual
SVG under `DesignSystem/Assets/Source/Glyphs/`. SVG filenames replace the
resource-ID dot with a hyphen (for example, `glyph.output-profile` maps to
`glyph-output-profile.svg`). Required presentation sizes are 16, 20, 24, and
32 px. Consumers supply semantic stroke and accessible labeling.

| Asset IDs | Profiles | Status |
|---|---|---|
| `glyph.overview`, `glyph.library`, `glyph.downloads`, `glyph.processing`, `glyph.history`, `glyph.accounts` | shared | Ready: optimized SVG plus Avalonia geometry. |
| `glyph.settings`, `glyph.tools`, `glyph.trash`, `glyph.add-books`, `glyph.scan`, `glyph.flight` | shared | Ready: optimized SVG plus Avalonia geometry. |
| `glyph.output-profile`, `glyph.process`, `glyph.completed`, `glyph.failed`, `glyph.retry`, `glyph.reveal-file` | shared | Ready: optimized SVG plus Avalonia geometry. |
| `glyph.metadata`, `glyph.filter`, `glyph.gallery`, `glyph.details`, `glyph.queue-log`, `glyph.marketplace` | shared | Ready: optimized SVG plus Avalonia geometry. |

## Status marks

Every key below is an original `StreamGeometry` normalized to 24 × 24 in
`StatusAssets.axaml`. Shape, literal text, and accessible name carry meaning;
profile color and optional seal/ring decoration are supplemental.

| Asset IDs | Profiles | Status |
|---|---|---|
| `status.download-pending`, `status.downloading`, `status.downloaded`, `status.processing`, `status.completed` | shared | Ready. |
| `status.failed`, `status.cancelled`, `status.unavailable`, `status.needs-attention`, `status.connected` | shared | Ready. |

## Illustrations

| Asset ID | Source/key | Profiles | Status |
|---|---|---|---|
| `illustration.cellar.empty-library` | `IllustrationAssets.axaml` | Cellar | Ready code-native template. |
| `illustration.shared.account-connection` | `IllustrationAssets.axaml` | shared | Ready code-native template. |
| `illustration.cellar.add-books` | `IllustrationAssets.axaml` | Cellar | Ready code-native template. |
| `illustration.tasting-room.add-books` | `IllustrationAssets.axaml` | Tasting Room | Ready code-native template. |
| `illustration.cellar.empty-decanter` | `IllustrationAssets.axaml` | Cellar | Ready code-native template. |
| `illustration.tasting-room.empty-decanter` | `IllustrationAssets.axaml` | Tasting Room | Ready code-native template. |
| `illustration.shared.processing-complete` | `IllustrationAssets.axaml` | shared | Ready code-native template. |
| `illustration.shared.no-search-results` | `IllustrationAssets.axaml` | shared | Ready code-native template. |
| `illustration.shared.offline-auth-attention` | `IllustrationAssets.axaml` | shared | Ready code-native template. |
| `illustration.cellar.bottle-rack-motif` | none | Cellar | Deferred: pure decoration, unnecessary for a complete layout, and contrary to the restrained first-release material treatment. |
| `illustration.tasting-room.still-life` | none | Tasting Room | Deferred: pure lifestyle decoration with no state meaning and avoidable alcohol-brand emphasis. |

All functional templates use a 96 × 72 internal composition canvas and scale
through a `Viewbox`. They contain no embedded copy, fake covers, or interaction.

## Optional texture

`texture.cellar.grain-seamless` is deferred. It is optional in the source
contract; semantic surfaces, borders, gradients, and elevation already carry
the Cellar material language without a raster dependency.

## Non-shipping visual evidence

`docs/design/asset-contact-sheet.svg` is a source-renderable evidence artifact,
not an Avalonia or package resource. It defines all 24 glyph and 10 status masters
from the production dictionaries and renders each at exact 16, 20, 24, and 32
logical-pixel boxes in four panels:

- Cellar semantic colors;
- Tasting Room semantic colors;
- High Contrast semantic colors;
- Decoration Off using the Cellar functional palette with decoration opacity
  explicitly recorded as zero.

That is 544 visible asset/size/profile instances. Status colors mirror the current
`StatusBadge` mapping, while geometry and literal resource IDs make color
redundant. The SVG was XML-validated and rendered with macOS Quick Look into a
temporary PNG for visual inspection on 2026-08-30. All four panels, labels, size
columns, strokes, and geometries were visible with no clipping observed. The PNG
is intentionally not committed. This proves the source-renderable contact-sheet
surface only; it is not an Avalonia runtime, DPI-scaling, or installed-package
claim.

## Legacy exclusions and blockers

- The current wine-glass app icon traces to a now-unavailable Flaticon source in
  `Source/LibationWinForms/Resources/_icon how to.txt`; no saved license record
  was found. It is not a source for this asset library.
- Current stoplight and PDF sources point to Flaticon in
  `Source/LibationWinForms/Resources/stoplight source.url` and
  `Source/LibationWinForms/Resources/pdf source.url`. They are not reused here.
- `DolbyAtmosLogoVertical`, message-box icons, cover placeholders, and platform
  badges are outside this contract.
- The original app-icon master has replaced the known ICO/ICNS/Linux SVG
  package inputs, and `AssetResources.axaml` is wired into `App.axaml`.
  Installed taskbar, Dock, launcher, and installer presentation remains a
  separate runtime/package proof tier and is not asserted here.

The machine-readable equivalent is `docs/design/asset-manifest.json`.
