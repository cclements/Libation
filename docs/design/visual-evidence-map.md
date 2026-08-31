# Contemporary Cellar visual evidence map
This report closes the visual-asset intake gate for the Contemporary Cellar work. Source paths are relative to `libation-contemporary-cellar-complete-agent-pack/`. Every image below was opened at original resolution on 2026-08-30, and every SHA-256 value was verified against `08-manifests/SHA256SUMS.txt`. The complete checksum manifest passed.

## Files verified

| Profile | Role | Source | Dimensions | SHA-256 |
|---|---|---|---:|---|
| Cellar | Primary composition | `02-reference-mockups/01-cellar-main-dashboard.png` | 1448 × 1086 | `a32439cf2515618519d430e1d02a538009e7162f6e6c7e8ac9e5606900db6780` |
| Tasting Room | Primary composition | `02-reference-mockups/02-tasting-room-main-dashboard.png` | 1448 × 1086 | `f5e36d3978f22efe91845b3418f52e8ef11fd60f4b574b7909039d3a35811e7d` |
| Cellar | Full design-system board | `03-design-system-boards/03-cellar-design-system-board.png` | 1448 × 1086 | `5fa3370e12c9d51e262208e0530e43360a9e745cd3e1f83fa02a7bb884ffba7c` |
| Tasting Room | Full design-system board | `03-design-system-boards/04-tasting-room-design-system-board.png` | 1448 × 1086 | `9906fa3425782b749abe70a7a17b856eab2568e241e04bd1f2ea10fb6882fbbe` |
| Cellar | Palette and materials crop | `04-visual-reference-crops/cellar/01-palette-and-materials.png` | 1448 × 265 | `73ed2846a1a430e6b0a42984de90377fff7d0cd927aa2de161911c9145b3d08c` |
| Cellar | Glyph and icon crop | `04-visual-reference-crops/cellar/02-glyph-and-icon-set.png` | 1448 × 235 | `45dc5825352855c90f2ce58d2a157339868bb1cb68a094cbdb723652ba49e7bf` |
| Cellar | Status-seal crop | `04-visual-reference-crops/cellar/03-status-seals.png` | 520 × 245 | `5ae5afb0a4bf892c947032a31a8769de207b9f9e3fc8b27db8491faf96b598c1` |
| Cellar | Decorative-illustration crop | `04-visual-reference-crops/cellar/04-decorative-illustrations.png` | 928 × 245 | `1ecaebbf307bc400a693a09a32dec3dacaa863b079b28d23bddb42c62e758923` |
| Cellar | Component-kit crop | `04-visual-reference-crops/cellar/05-component-kit.png` | 1448 × 385 | `1e69e2712774e48a7fcb49d5aa103da20558f23362e33946ba471e6c47fb8947` |
| Tasting Room | Brand and palette crop | `04-visual-reference-crops/tasting-room/01-brand-and-palette.png` | 1448 × 205 | `22991cb0886eb735ee262f7fb1fc0070ffe9f8770b1238cb772d48d98275b8ac` |
| Tasting Room | Materials and textures crop | `04-visual-reference-crops/tasting-room/02-materials-and-textures.png` | 605 × 245 | `7f481a6f52811efd2541c28b2f10e120cb122803c5f9a89b94d76e73e67d1de3` |
| Tasting Room | Glyph and icon crop | `04-visual-reference-crops/tasting-room/03-glyph-and-icon-set.png` | 843 × 425 | `7f642598fcfe5e0682871055fc744cd7cf9127a706a48b1ab06265a537ce0672` |
| Tasting Room | Status-seal crop | `04-visual-reference-crops/tasting-room/04-status-seals.png` | 605 × 205 | `f07c534272f0c1bf7ba1865afe77e073d9c0450cd73a662dae83caad25bc117e` |
| Tasting Room | Decorative-illustration crop | `04-visual-reference-crops/tasting-room/05-decorative-illustrations.png` | 605 × 390 | `5575ae3327247afb6a4d2e5eb704c767604d3f56910aedcd9ec303b437d461b9` |
| Tasting Room | Component-kit crop | `04-visual-reference-crops/tasting-room/06-component-kit.png` | 843 × 390 | `42b3d069921c40693fadb0ca574ed43db4139d6df5d4c87bc9cf7194bff11fbb` |
| Tasting Room | Platform-badge crop | `04-visual-reference-crops/tasting-room/07-platform-badges.png` | 900 × 101 | `215e8ffd45c7c93f9edabebe732a9971ea9d56568c4277b20e4d007f5c0bd034` |

## Current upstream seams

The reference compositions describe a future shell, not the current control tree. Production work must preserve these existing ownership seams while the contemporary shell remains behind its feature control:

| Current surface | Current source | Contemporary use |
|---|---|---|
| Window, native/in-window menus, search, status, and layout host | `Source/LibationAvalonia/Views/MainWindow.axaml` | Transitional window host for `AppShellView`; keep native chrome and forward existing commands. |
| Details library and its power-user behavior | `Source/LibationAvalonia/Views/ProductsDisplay.axaml` | Rehost as Library Details mode; Gallery and cards must share its authoritative selection/command adapters. |
| Processing queue and log | `Source/LibationAvalonia/Views/ProcessQueueControl.axaml` | Rehost or summarize the one existing queue. Never create or display a second queue source/control. |
| Theme/control preview | `Source/LibationAvalonia/Controls/ThemePreviewControl.axaml` | Extend into the scoped component/profile gallery rather than creating a parallel preview. |
| Light/Dark resources and Fluent styles | `Source/LibationAvalonia/App.axaml` | Bridge through semantic profile resources; feature views do not consume raw board colors. |
| Existing vector geometry | `Source/LibationAvalonia/Assets/LibationVectorIcons.xaml` | Audit/reuse as interim geometry, then expose stable semantic asset IDs. |

## Cellar visual thesis

- **Composition:** library-first working surface, full rail, compact metrics, persistent Current Flight at wide widths, and a docked Decanter.
- **Materials:** black cherry, burgundy, parchment, walnut-toned value shifts, and restrained brass structural accents.
- **Typography:** platform UI sans for controls and dense data; editorial serif only for page or featured book titles.
- **Density:** compact, information-rich desktop layout with clear focus and hit targets.
- **Signature surfaces:** rich cover presentation, compact status treatment, a right-side Flight tray, and a bottom processing dock.
- **Restraint applied:** reduce literal skeuomorphism by roughly one third. No photographic wood everywhere, ornate frames, leather behind text, faux-antique controls, or persistent liquid animation. Use at most one purposeful cellar/decanter illustration in a relevant state.

## Tasting Room visual thesis

- **Composition:** overview-first editorial workspace with a light rail, top metric strip, Current Flight card/list, compact Decanter, recent library rows, and Add Books action.
- **Materials:** cream/limestone surfaces, burgundy action color, sage success, pale-oak detail, soft borders, and shallow neutral elevation.
- **Typography:** platform UI sans for operational content with restrained serif titles/section headings.
- **Density:** generous but still desktop-appropriate; functional data and commands remain above the fold.
- **Signature surfaces:** Today’s Selection header, metric strip, paired Flight/Decanter region, and one restrained crate, decanter, or still-life illustration.
- **Restraint applied:** retain desktop-application density. Do not turn the surface into a lifestyle web page, place controls over photography, hide operations in empty space, or use low-contrast editorial text.

## Reference-to-production map

| Screen or component | Profile | Visible source region or focused crop | Preserve | Production restraint | Production surface, token, or asset ID | Known ambiguity |
|---|---|---|---|---|---|---|
| Shell and navigation rail | Cellar | Cellar dashboard left rail; Cellar board glyph row and component-kit nav item | Full dark rail, selected marker, literal destination labels, lower utilities | Walnut is a tonal surface, not a photographic cabinet; brass is an accent line | `AppShellView`, `LibationNavigationRail`, `glyph.overview`, `glyph.library`, `glyph.downloads`, `glyph.processing`, `glyph.history`, `glyph.settings`, `glyph.tools`, `glyph.trash` | Board icon strokes and spacing are direction only; command parity and native menus remain authoritative. |
| Shell and navigation rail | Tasting Room | Tasting Room dashboard left rail; Tasting glyph and component crops | Light rail, burgundy selection, crisp line glyphs, operational hierarchy | Avoid marketing-site whitespace and logo-dominated navigation | Same shell/rail API and glyph IDs as Cellar, with profile resources/templates | The generated Today’s Selection grape glyph is not exact production geometry. |
| Palette and material system | Cellar | `cellar/01-palette-and-materials.png` | Near-black canvas, layered warm surfaces, burgundy, parchment, restrained brass and semantic status colors | Render via semantic brushes, borders, gradients, and shadows; no board texture fragments | `Libation.Color.*`, `Libation.Brush.*`, shape/elevation/density tokens | Printed hex labels in the board are visual evidence only; Plan §10.2 values are the engineering starting points and contrast may adjust them. |
| Palette and material system | Tasting Room | `tasting-room/01-brand-and-palette.png`; `tasting-room/02-materials-and-textures.png` | Cream/limestone hierarchy, burgundy action, sage success, pale-oak detail, charcoal text | Keep surfaces quiet and readable; avoid blurred glass and material samples behind text | Same semantic token keys with Tasting Room values | Board material swatches do not require literal wood, stone, metal, glass, or paper images. |
| Page header and metrics | Cellar | Cellar dashboard title/search/metric row; Cellar component crop stat tile/search | Compact title, global search, useful library/processing aggregates | Values come from live data; do not duplicate the board’s count set or labels | `PageHeader`, `MetricCard`, semantic input/card styles | Which metrics are available and inexpensive is determined by the shared dashboard data contract. |
| Today’s Selection overview | Tasting Room | Tasting Room dashboard title, top metric strip, first content row | Editorial hierarchy, four-up-at-wide metric rhythm, attention before secondary content | Greeting is optional; first paint and operational data must not depend on art | Tasting `DashboardView`, `PageHeader`, `MetricCard`, `AttentionBanner` | Exact card count, greeting, and metric values are not requirements. |
| Cellar library composition | Cellar | Cellar dashboard central gallery and search/header region | Library remains hero; rich covers, compact metadata/status, visible selection | No fake covers or generated metadata; Gallery must virtualize | Existing `ProductsDisplay` for Details; future virtualized Gallery with `BookCard` | The board shows a five-card example, not a fixed column count or card dimension. |
| Recent/library rows | Tasting Room | Tasting dashboard “From Your Cellar”; Tasting component crop row/table examples | Quiet dense rows, search/filter access, readable metadata | Preserve current DataGrid power features; avoid card-on-card nesting | Existing `ProductsDisplay`, `BookRow`, shared Library command header | Generated pagination, dates, titles, and row counts are not required behavior. |
| Current Flight | Cellar | Cellar dashboard right tray; Cellar component crop Flight panel | Persistent wide tray, ordered selected titles, aggregate count/size, clear/process access | No second selection model; no corkscrew/grape ornament as sole meaning | `FlightService`, `FlightTray`, `glyph.flight` | Exact tray width, sample title order, and capacity are not requirements. |
| Current Flight | Tasting Room | Tasting dashboard center-left card/list; Tasting component crop Flight card | Dashboard summary plus drawer, row progress and direct Processing access | Same selection service and commands as Cellar; decorative glyph is optional | Same `FlightService` and shared Flight controls with Tasting composition | Board progress appears to mix selection and processing; product semantics remain Plan §9.3. |
| Decanter summary and Processing | Cellar | Cellar dashboard bottom dock; Cellar decorative/component crops | Docked aggregate processing surface, clear stage/progress/status, bounded decoration | Preserve the single current queue; no ornate wood frame or continuous liquid motion | Existing `ProcessQueueControl` through queue adapter; `DecanterSummary`, `QueueItem`, `glyph.processing`, `glyph.queue-log` | The illustrated decanter scale and liquid level do not define progress math. |
| Decanter summary and Processing | Tasting Room | Tasting dashboard compact card; Tasting decorative/component crops | Compact status card, clear current job and progress, expandable full workspace | Vessel art is optional and nonessential; no controls over art | Same queue adapter and Processing route; Tasting `DecanterSummary` template | Generated stage names, formats, percentages, and quality-check sequence are illustrative. |
| Status badges and marks | Both | Both status-seal crops and glyph rows | Stable semantic icon shape, literal status text, accessible name, non-color cue | Wax/ring treatments are optional decoration; never use a seal or color alone | `StatusBadge` and all `status.*` IDs in the production asset contract | Board uses a subset and inconsistent terms; canonical product statuses come from Plan §11.6 and live queue state. |
| Add Books and empty library | Cellar | Cellar dashboard sidebar door; Cellar decorative/component crops | Clear browse/drop action and one atmospheric empty-state cue | Replace literal wood cabinet with restrained vector/tonal composition; text remains live UI | `DropZone`, `EmptyState`, `illustration.cellar.empty-library`, `illustration.cellar.add-books`, `glyph.add-books` | “Door” and “bottle rack” are motifs, not mandatory container shapes. |
| Add Books and empty library | Tasting Room | Tasting dashboard lower-right crate card; Tasting decorative/component crops | Prominent accessible browse/drop action, light editorial composition | Crate/still life is optional; drop interaction and literal copy remain primary | `DropZone`, `EmptyState`, `illustration.tasting-room.add-books`, `illustration.tasting-room.still-life` | Exact crate, books, bottle, glass, plant, and shelf arrangement is not a requirement. |
| Buttons, inputs, menus, progress, cards, rows, and toast | Both | Both component-kit crops | Shared hierarchy/state language with distinct profile templates | Components must cover focus, disabled, loading, empty, error, density, High Contrast, and Decoration Off | Shared component/style library and `ToastHost`; semantic resources only | Board examples omit many required states; they are visual direction, not the state matrix. |
| Brand and app icon | Both | Cellar board header/glyph crop; Tasting brand/palette and glyph crops | Book-plus-vessel idea, small-size legibility, one-color and light/dark families | Redraw as reviewed vector geometry; do not trace or crop generated marks/lettering | `brand.app-icon`, `brand.mark.*`, `brand.wordmark.*` | Final silhouette, wordmark typography, platform exports, and trademark review remain asset-production decisions. |
| Secondary destinations | Both | Dashboard rails and both glyph crops | Same routes and literal labels for Downloads, History, Accounts, Settings, Tools, and Trash | Do not infer new data or controls from tiny board icons | Shared shell routes; corresponding `glyph.*`; existing dialogs/commands remain authoritative | Accounts, Trash, and marketplace visuals are not fully composed in the boards. |
| Platform packaging/presentation | Both | Cellar component crop lower edge; `tasting-room/07-platform-badges.png` | Cross-platform support can be stated where actually supported | Do not crop board logos, invent platform endorsement, or replace package-native icon rules | Existing Windows `.ico`, macOS `.icns`, Linux SVG/package scripts; reviewed platform marks where legitimately needed | The crop is direction for a footer/about surface, not a requirement to show badges in the primary shell. |
| Profile and component preview | Both | Both design-system boards and component crops | Side-by-side representative components under profile scope | Preview must not mutate global theme or user grid settings | Extend `ThemePreviewControl`; render `ThemePreviewCard` and component gallery | Full board density is a reference contact sheet, not a runtime screen to reproduce. |

## Ambiguities and explicit non-requirements

- Generated text and lettering are not product copy. Misspellings, taglines, headings, menu labels, version strings, and decorative plaque text in the images are ignored unless separately specified by the plan or existing product language.
- Fake book covers, titles, authors, narrators, dates, formats, paths, and metadata are not fixtures or shippable content.
- Numeric values are not requirements: counts, storage amounts, durations, percentages, ETAs, chapter numbers, pagination, and progress positions must come from real state.
- Small generated geometry artifacts are not requirements: uneven strokes, malformed glyph details, inconsistent dots/rings, spacing errors, ornamental ends, window-chrome details, and tiny misalignments must be redrawn or replaced by platform-native behavior.
- The 1448 × 1086 compositions and crop dimensions establish evidence identity, not runtime pixel specifications or responsive breakpoints.
- Material swatches establish atmosphere. Walnut, limestone, leather, glass, brass, paper, crate wood, glow, and shadow do not mandate photographic textures.
- Functional labels remain primary. Wine language is supportive and never replaces error, cancellation, authentication, or recovery meaning.
- **No user-provided or pack/reference PNG ships in the application or platform packages.** The verified PNGs remain design evidence only; no crop, fake UI, raster text, fake cover, or generated lettering may be embedded.

## Gate result

- **PASS**
- All required references opened at original resolution.
- All recorded dimensions are readable.
- `08-manifests/SHA256SUMS.txt` verified without mismatch.
- Blockers: none.
