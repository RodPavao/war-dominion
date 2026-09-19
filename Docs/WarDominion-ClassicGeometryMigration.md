# WAR DOMINION — Classic 16:9 Geometry Migration

## Pilot audit — 2026-09-11

Status: **PARTIAL**. Baseline: **707/707 EditMode** reported by Unity before this
pilot. The Classic remains on `ClassicMap.png`; the WD candidate was not promoted.

The corrected candidate
`Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png` is a valid
1920×1080 multiple-sprite asset with GUID
`2a356458fc2283749aa4aee4250932bd` and a full-canvas SpriteRect. The active art is
still `ClassicMap.png` (1365×1024, GUID
`f328fe402440c014b88800328223fa03`). Pixels were not changed.

The existing scene/definition audit still records 42 territories, six regions and
80 logical connections. No ID, region membership, bonus or connection was changed.
All 42 current territory sprites and polygon paths remain aligned to the old
1365×1024 composition; counters retain their existing per-territory offsets and
`SelectionScaleRoot` continues to derive its center from the effective collider.

### Global diagnostic relation

The best coarse relation retained from the dark-territory bounding-box comparison is:

- scale X: `1.0986257074`;
- scale Y: `1.0671232877`;
- pixel offset X: `210.1374293`;
- pixel offset Y: `24.1068493`;
- affine/shear: not justified by the available deterministic data.

This relation was **not applied**. The new artwork has no set of 42 authoritative
territory masks from which to measure border-by-border correspondence. Consequently,
mean and maximum per-territory residuals are not defensibly measurable yet. Treating
the coarse bounding-box fit as an exact registration could move sprites and colliders
away from their actual borders.

Classification for this pilot: `OK_GLOBAL = 0`, `MINOR_ADJUSTMENT = 0`,
`OUTLIER = 0`, `NEEDS_MANUAL_REVIEW = 42`. This is deliberately conservative rather
than a claim that all 42 territories are geometric outliers.

### Assisted review added to the existing Map Editor

The `Migração16x9` tab loads the Classic WD candidate, overlays it on the active art,
allows opacity/visual scale/offset tuning, reset, territory selection and temporary
manual-review marking. It displays the diagnostic relation and keeps global Apply
disabled while residuals are unvalidated. Preview changes are window-session state:
they do not dirty the map, scene or artwork. Existing counter and region-label edits
continue to use Unity Undo/Redo.

No sprite, mask, transform, collider, counter, scene or `DefinicaoMapa` was mutated by
the pilot. Promotion remains gated on 42 reviewed masks/colliders, acceptable counters,
centralized selection behavior and a successful Play Mode pass.

## Semi-automatic fitting pass — 2026-09-11

`WarDominionMapMigrationFitter` now performs an explicit, cached-on-demand analysis.
It reads the existing alpha mask as a seed, extracts and caps its boundary samples,
builds an in-memory luminance/contrast edge map from the WD artwork, applies the global
seed and searches only `±16 px` in `4 px` steps with local X/Y scale factors
`0.97 / 1.00 / 1.03`. The score combines local edge proximity, in-canvas coverage,
distance from the global seed and limited-area/scale preservation. No texture derived
from this process is saved.

Confidence thresholds are deterministic: `HIGH >= 0.72`, `MEDIUM >= 0.52`,
`LOW >= 0.32`, otherwise `UNRESOLVED`. The first read-only Classic run produced:

- HIGH: 28;
- MEDIUM: 14;
- LOW: 0;
- UNRESOLVED: 0.

MEDIUM territories requiring visual review: Afghanistan (`0.6094`), Alberta
(`0.5652`), China (`0.6789`), Congo (`0.6522`), EastUSA (`0.7181`), India
(`0.7176`), Irkutsk (`0.5935`), Mongolia (`0.7001`), Ontario (`0.6129`), Peru
(`0.6374`), Siberia (`0.6604`), Ural (`0.6806`), WestUSA (`0.6544`) and Yakutsk
(`0.6624`). There are no LOW/UNRESOLVED entries to list from this run.

The result remains preview-only. HIGH can be batch-marked as reviewed, while LOW and
UNRESOLVED are excluded from individual/batch acceptance. No scene geometry is applied
before the producer reviews the overlay; therefore applied runtime territories remain
`0`, preview results are `42`, and Classic stays `PARTIAL`.

Audit date: 2026-09-10. Status: **automatic migration stopped before mutation**.

## Safety gate result

The requested authority
`Assets/Mapas/Classic/Arte/WD_Map_Classic_Background_16x9_v01.png` does not
exist. The similarly named imported file is
`Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png`.
It measures **1920×1081**, not the required **1920×1080**. Both explicit gates in the
migration brief therefore fail. No scene, definition, transform, sprite, collider,
counter or importer was changed.

## Art identities

| Role | Path | GUID | Dimensions | Status |
|---|---|---|---:|---|
| Current authority | `Assets/Mapas/Classic/Arte/ClassicMap.png` | `f328fe402440c014b88800328223fa03` | 1365×1024 | active |
| Old test | `Legacy/Assets/Maps/Classic/Variants/ClassicMap_16x9_TEST.png.png` | `f330bbadf648eab41ad8ac1cb9f17140` | 1672×941 | historical variant outside Assets |
| Similar new import | `Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png` | `2a356458fc2283749aa4aee4250932bd` | 1920×1081 | blocked pending corrected authority |

The similar new import currently has zero serialized scene/prefab/asset consumers.

## Importer comparison

Both current and similar-new files use Texture Type Sprite, Sprite Mode Multiple,
100 PPU, tight mesh, bilinear filtering, clamp wrap, sRGB and alpha transparency. The
current art uses uncompressed platform texture settings; the similar-new import uses
default normal compression. The new importer contains one slice named
`WD_Map_ClassicMap_Background_16x9_v01_0`, rect 1920×1081. No setting was copied or
changed because the source identity/dimension gate failed.

## Current implementation architecture

- `Assets/WarGame.unity` contains exactly 42 GameObjects with `TerritorioClique`, one
  `PolygonCollider2D` and one `ContadorTropas` each.
- Each territory has its own sprite asset/GUID sliced from the current Classic source.
  The `SpriteRenderer` and generated polygon collider therefore share the old-map
  coordinate geometry.
- All territory roots currently share local position `(0.55,-0.12,0)`, scale
  `(1.04022,0.96932,1)` and identity rotation. Geometry is encoded mainly in each
  full-map-aligned sprite and polygon paths, not by 42 independent root positions.
- Counters are children with individual local positions. Runtime presentation uses
  `TerritorioVisual`, a selection-scale root and `WDArmyCounterView` without changing
  authoritative territory identity.
- The background is `classic_0/SpriteRenderer`, still referencing the current art.

This proves that a simple background swap is insufficient: the 42 sliced sprites and
their polygon paths still describe the old 1365×1024 composition. A reliable migration
requires a corrected 1920×1080 authority and an objective image-to-image mapping or
new masks; approximate coast reconstruction is explicitly outside scope.

## Diagnostic global relation — not applied

A thresholded bounding-box comparison of dark territory interiors produced this rough
pixel-space relation:

- old bounds: `(100,58)` to `(1337,788)`;
- similar-new bounds: `(320,86)` to `(1679,865)`;
- approximate `scaleX = 1.0986257074`;
- approximate `scaleY = 1.0671232877`;
- approximate top-left pixel offset `(210.1374293,24.1068493)`.

This is only a coarse diagnostic. It is not a safe affine registration: coastline and
internal-border residuals were not measured, the canvas has the wrong height, and the
new importer slice/pivot differs in practical geometry. No parameter was applied.

## Reversible structural baseline

- Classic definition SHA-256:
  `3F28AE401223ACE5E521048631D17C5B2E637817CAEA13885DFEBC0B7A4C97E1`
- Current working `WarGame.unity` SHA-256:
  `1E8DC44914C0C771C4744B200C89FB67EE26290A74545E48D6969F25A79AC1C5`
- Logical territories: 42.
- Regions: 6.
- Serialized logical connections: 80.
- Territory IDs:
  `Afghanistan`, `Alaska`, `Alberta`, `Algeria`, `Arabia`, `Argentina`, `Brazil`,
  `China`, `Congo`, `EastAustralia`, `EastUSA`, `Egypt`, `Ethiopia`, `Germany`,
  `Greenland`, `Iceland`, `India`, `Indochina`, `Indonesia`, `Irkutsk`, `Italy`,
  `Japan`, `Kamchatka`, `Madagascar`, `Mexico`, `Mongolia`, `NewGuinea`, `Ontario`,
  `Peru`, `Quebec`, `Russia`, `Scandinavia`, `Siberia`, `SouthAfrica`, `Spain`,
  `UnitedKingdom`, `Ural`, `Venezuela`, `WestAustralia`, `WestUSA`, `Yakutsk`, `Yukon`.

## Per-territory original geometry

`Sprite` is the territory-specific sprite GUID. Every row uses a `PolygonCollider2D`
generated/serialized against that sprite. Root transform for every row is
`pos 0.55,-0.12,0 / scale 1.04022,0.96932,1 / rotation 0,0,0,1`.

| Territory/ID | Sprite GUID | Counter local position |
|---|---|---|
| Afghanistan | `7e8edffd57ed2994d9f7d29aa749c854` | `2.73,2,0` |
| Alaska | `35f69f06e9a020f43843177eba8e8cc4` | `-5.15,3.72,0` |
| Alberta | `64fc6eaa87289c6408716491483ebba2` | `-4.212,2.898,0` |
| Algeria | `5aabda84ba94d174b88f99ced597ca4d` | `-0.07,0.36004,0` |
| Arabia | `d8b1cef7c1d220d4da8a9d74540a5063` | `1.77,1.08,0` |
| Argentina | `08935db5526f4d5459165fc4dcf422ab` | `-2.73,-1.88,0` |
| Brazil | `336ca9e3816cc9a4791e6d849cfefc98` | `-2.07,-0.58,0` |
| China | `29799a90bfea27c478722b6d0b7ff9fe` | `4.04,1.52,0` |
| Congo | `406a95f7e1c033a4faa6b61ed1e51d0c` | `0.74833333,-0.67809516,0` |
| EastAustralia | `5f2988fcffb948142b82d2f959c63529` | `6.02,-1.98,0` |
| EastUSA | `3ea566e2bdb0e674bbf701b3f3309f56` | `-3.32,1.97,0` |
| Egypt | `bc50ddb8423215e479b2dc76e52c5e3d` | `0.87,0.68,0` |
| Ethiopia | `695c78c5842d5c24fabdd8b1c3c833d6` | `1.44,-0.35,0` |
| Germany | `7f0600e5f1d256149b90012c69f2cbdf` | `0.4742307,2.4853845,0` |
| Greenland | `4210b83ca86d76b4095e80b3400f8e27` | `-1.8289472,4.019211,0` |
| Iceland | `bfeb4494a3b57d9449cfdc04746f06c4` | `-1.045,3.34,0` |
| India | `a015edf28e615c540876ceb4dc460e46` | `3.38,0.85,0` |
| Indochina | `ed0b7b5726a5c4748a953fdf8c9339bd` | `4.32,0.51987,0` |
| Indonesia | `c599b59428a2c5244b593a74d6d91aa0` | `4.69,-0.71,0` |
| Irkutsk | `ebe580e5b1304814e8c1df5b8c55eb2b` | `4.28,3.12,0` |
| Italy | `8635233f864953d43b103114d35f3712` | `0.59,1.94,0` |
| Japan | `4119a49a53a1e2044a98a5dcda24bd3f` | `5.745,1.81,0` |
| Kamchatka | `07776e4f8affeba4ab5c16c57ec355c4` | `5.38,3.79,0` |
| Madagascar | `f677b9d6fab598b44b2f5d9993300ccd` | `1.965,-1.55,0` |
| Mexico | `c3a1e2b8b2739f144ab40c952ba536df` | `-4.025,1.08,0` |
| Mongolia | `6725d5711ad3740438b377e8d56cdc4b` | `4.66,2.34,0` |
| NewGuinea | `d52604fe4a3d2aa4bb0dfbaf949bb92a` | `5.885,-0.92,0` |
| Ontario | `a2e60fdd9bbd0a84fbdb61f879491812` | `-3.39,2.84,0` |
| Peru | `a55860eb13993d34bbfc02fa46b88336` | `-2.7679412,-0.95027,0` |
| Quebec | `8ec1b115aa5650a4f9b9c2ec9e4ac4bb` | `-2.62,2.7903442,0` |
| Russia | `59b8cbe91e4640d49a7438949e156967` | `1.54,2.68,0` |
| Scandinavia | `aabe3d776c551e145953fc4dd99d504c` | `0.51,3.36,0` |
| Siberia | `d32f80c59f43c9440b159aedc2635112` | `3.47,3.27,0` |
| SouthAfrica | `fb49f8790d3085d43ac46da959a47c6b` | `0.9853571,-1.65,0` |
| Spain | `f8447c6e865c64f4b84969e17753fc71` | `-0.37,1.7,0` |
| UnitedKingdom | `29223390445a8274e87562a0647f1782` | `-0.435,2.66,0` |
| Ural | `d148a39b80d0e244f868fe61710bfc3c` | `2.68,3.2,0` |
| Venezuela | `17df1bd843cc7704499f2add5f72318c` | `-3.08,0.1,0` |
| WestAustralia | `7801ac7d6df15954c86c69e04a7259da` | `5.276111,-1.8462961,0` |
| WestUSA | `e1a5beaab1e2d804b8509c9462829076` | `-4.22,2.06,0` |
| Yakutsk | `3b9656b8cb3b0f4409dca5441861f5e1` | `4.3810863,3.8465214,0` |
| Yukon | `553270d5529984a499ddd98ecdaac7dd` | `-3.89,3.64,0` |

## Before/after comparison

There is no after-state because the safety gate stopped migration. The before hashes,
42 IDs, 80 connections and six regions remain the comparison baseline for the assisted
step. Required next input is a Unity-imported file with the exact authorized identity
and actual 1920×1080 dimensions. Once supplied, registration should measure per-border
residuals before deciding between a global transform and regenerated masks.
