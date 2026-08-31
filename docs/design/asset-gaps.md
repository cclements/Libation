# Contemporary Cellar production asset gaps

This is the post–Prompt 03 gap ledger for the production asset contract. The
code-native source surface lives under
`Source/LibationAvalonia/DesignSystem/Assets/`; human- and machine-readable
provenance records live in `asset-manifest.md` and `asset-manifest.json`.

## Resolved in the asset source contract

| Contract area | Resolution | Evidence |
|---|---|---|
| Six brand IDs | Original open-book/carafe mark geometry plus light/dark runtime wordmark templates; no legacy wine-glass geometry reused. | `BrandAssets.axaml` |
| Twenty-four core glyph IDs | Every ID has a distinct 24 × 24 `StreamGeometry` key and an individual optimized SVG source/export. | `GlyphAssets.axaml`; `DesignSystem/Assets/Source/Glyphs/*.svg` |
| Ten status IDs | Every ID has a distinct 24 × 24, non-color-only `StreamGeometry` key. | `StatusAssets.axaml` |
| Nine functional illustration IDs | Text-free, code-native 96 × 72 `ControlTemplate` masters using semantic brushes. | `IllustrationAssets.axaml` |
| Stable consumption surface | One resource dictionary includes all production asset dictionaries. | `AssetResources.axaml` |
| Provenance and usage | Source, license, profiles, sizing, deferral rationale, and package boundary recorded. | `asset-manifest.md`, `asset-manifest.json`, `asset-usage.md` |
| Platform icon sources | One original open-book/carafe SVG master exported to all known Windows ICO, macOS ICNS, and Linux SVG consumers. | `DesignSystem/Assets/Source/brand-app-icon.svg`; package inputs |
| Glyph/status contact sheet | All 24 glyphs and 10 statuses rendered at 16/20/24/32 in Cellar, Tasting Room, High Contrast, and Decoration Off; temporary render visually inspected. | `docs/design/asset-contact-sheet.svg` |
| Runtime wordmark lockups | Vector mark plus live accessible text satisfies every current shipping consumer; no outlined static export is required. | `BrandAssets.axaml`; `asset-usage.md` |
| Component asset seams | Queue fallback now consumes contract-backed `glyph.processing`; `status.connected` is reachable through `LibationStatusKind.Connected` and the Success semantic brush. | `QueueItem.axaml`; `ComponentModels.cs`; `StatusBadge.axaml` |

## Explicitly deferred

| Asset ID | Rationale | Layout contract |
|---|---|---|
| `illustration.cellar.bottle-rack-motif` | Pure decoration would add avoidable first-release skeuomorphism. | No required layout space or behavior may depend on it. |
| `illustration.tasting-room.still-life` | Pure lifestyle decoration conveys no product state and adds avoidable alcohol-brand emphasis. | No required layout space or behavior may depend on it. |
| `texture.cellar.grain-seamless` | Optional in the source contract; semantic code-native surfaces already carry the material direction. | Cellar remains complete with no raster texture. |

These deferrals satisfy the asset contract’s requirement that unresolved IDs be
explicit and reasoned; they are not silent placeholders.

## Remaining integration and evidence gaps

| Gap | Owner/boundary | Required closure evidence |
|---|---|---|
| Avalonia runtime and DPI visual review | Visual QA. The source SVG proves exact logical target sizes, not Avalonia rasterization or display scaling. | In-app review at 100/125/150/200% on supported platforms, including High Contrast and Decoration Off. |
| Installed-artifact proof | Platform QA/release owner. Source files and frames are verified, but packages were not built. | Taskbar, Dock, launcher, installer, and packaged-resource evidence from each supported platform. |

No package, visual-runtime, cross-platform, or installed-artifact claim is made
from the file-level exports alone.

## Legacy provenance boundary

- The replaced app-icon/wine-glass family traced to a now-unavailable Flaticon
  source named in `Source/LibationWinForms/Resources/_icon how to.txt`; no saved
  license record was found. It was not used as a source for the new mark.
- Existing stoplight and PDF files also point to Flaticon sources. No geometry
  from those assets is used in the new status vocabulary.
- `DolbyAtmosLogoVertical`, message-box icons, current cover placeholders, and
  platform badges remain outside the Contemporary Cellar asset contract.

## Shipping boundary

No PNG from the complete agent pack was copied into the application or any
package. Brand, glyph, and status masters are vectors. Functional illustrations
are code-native vector templates with no embedded text, fake UI, or fake covers.
Decoration Off can omit every illustration without removing state, copy,
commands, or focus targets.
