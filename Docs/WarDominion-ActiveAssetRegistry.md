# WAR DOMINION — Active Asset Registry

Audit date: 2026-09-10. Unity safety baseline: 694/694 EditMode. This registry
records the current authorities without renaming, moving or modifying map content.
Paths are repository-relative; every Unity asset keeps its paired `.meta` and GUID.

## Map authority chain

Runtime selection is `Resources/Mapas/CatalogoMapas.asset` → `DefinicaoMapa` →
`metadados.scenePath`. `MapaAtivo` loads the catalog through
`Resources.Load<CatalogoMapas>("Mapas/CatalogoMapas")`. Each definition also stores
`arteBase`; the five audited scenes reference that same art in their background
`SpriteRenderer`.

## Classic

- Logical Definition: `Assets/Resources/Mapas/Classic.asset`
- Definition GUID: `281691c8141c4498bcac247bcc960d45`
- Scene: `Assets/WarGame.unity`
- Scene GUID: `6cbfd99a172aaa346bd82db9c96bcf08`
- Active Background Art: `Assets/Mapas/Classic/Arte/ClassicMap.png`
- Art GUID: `f328fe402440c014b88800328223fa03`
- Dimensions/import: 1365×1024, multiple sprites, 100 PPU, alpha enabled
- Referenced By: `Classic.asset` and `classic_0/SpriteRenderer` in `WarGame.unity`
- Territory Geometry: 42 `TerritorioClique`, 42 `PolygonCollider2D`, 42
  `ContadorTropas`; territory sprites are sub-assets of the multiple-sprite Classic
  texture rather than separate files in a `Mascaras` directory. Runtime presentation
  also uses `TerritorioVisual`, `TerritorioContador` and `WDArmyCounterView`.
- Status: `ACTIVE_CANONICAL`
- Old test variant: moved to
   `Legacy/Assets/Maps/Classic/Variants/ClassicMap_16x9_TEST.png.png`
  (`f330bbadf648eab41ad8ac1cb9f17140`, 1672×941). It has zero Unity consumers and is
  retained outside `Assets` only as historical reference.
- Pending definitive import: the requested
  `WD_Map_Classic_Background_16x9_v01.png` is absent. A similarly named file,
  `WD_Map_ClassicMap_Background_16x9_v01.png`, exists with GUID
  `2a356458fc2283749aa4aee4250932bd`, now measures 1920×1080 and has a valid
  1920×1080 SpriteRect. It remains an `ACTIVE_MIGRATION_CANDIDATE`; it was not
  assigned because territorial geometry still requires manual review.
- Notes: do not replace the active art or recalibrate the 42 transforms/colliders in
  this organization step.
- Pilot status (2026-09-11): `PARTIAL`. The existing Map Editor has a non-destructive
  `Migração16x9` overlay for the WD candidate. No global transform was applied because
  per-territory residuals cannot be validated without authoritative candidate masks;
  `ClassicMap.png` remains the runtime authority.

## Pearl Harbor

- Logical Definition: `Assets/Resources/Mapas/PearlHarbor.asset`
- Definition GUID: `9db6228e379616845aea5a10e6206d29`
- Scene: `Assets/Mapas/Pearl Harbor/PearlHarbor.unity`
- Scene GUID: `2429d1e7f7723dd4a828fce2228525ec`
- Active Background Art: `Assets/Mapas/Pearl Harbor/Arte/PearlHarbor.png`
- Art GUID: `302937e338d81f04fbd3277e54f4a27f`
- Dimensions/import: 1281×852, single sprite, 100 PPU, alpha enabled
- Referenced By: `PearlHarbor.asset` and `PearlHarbor_Arte/SpriteRenderer`
- Territory Geometry: 45 PNG masks in `Assets/Mapas/Pearl Harbor/Mascaras/`, scene
  territory objects with collider/counter components, and positions stored in the
  definition/scene.
- Status: `ACTIVE_CANONICAL`
- Authoring references were moved, with their `.meta`, to
  `Legacy/Assets/Maps/PearlHarbor/Reference/`; both had zero Unity consumers.

## Batalha do Riachuelo

- Logical Definition: `Assets/Resources/Mapas/Riachuelo.asset`
- Definition GUID: `f886a0e963fbaca4281bec5f7c1c09a6`
- Scene: `Assets/Mapas/Batalha do Riachuelo/Riachuelo.unity`
- Scene GUID: `369fd9dcf27722543a7bdc9b9c939851`
- Active Background Art: `Assets/Mapas/Batalha do Riachuelo/Arte/Riachuelo.png`
- Art GUID: `6e22641b0342a464eb3cc732e765bb9a`
- Dimensions/import: 3015×2311, single sprite, 100 PPU, alpha enabled
- Referenced By: `Riachuelo.asset` and `Riachuelo_Arte/SpriteRenderer`
- Territory Geometry: 37 PNG masks in `Assets/Mapas/Batalha do Riachuelo/Mascaras/`,
  scene territory objects, colliders and counters.
- Status: `ACTIVE_CANONICAL`
- Authoring references were moved, with their `.meta`, to
  `Legacy/Assets/Maps/Riachuelo/Reference/`; both had zero Unity consumers.

## Peloponeso

- Logical Definition: `Assets/Resources/Mapas/Peloponeso.asset`
- Definition GUID: `e0f23e8c5d74bd644b47a4a5acffd074`
- Scene: `Assets/Mapas/Peloponeso/Peloponeso.unity`
- Scene GUID: `093f12f9aa25ec74aa634f0d9bf74a2f`
- Active Background Art: `Assets/Mapas/Peloponeso/Arte/Peloponeso.png`
- Art GUID: `0306bcfaf8cad64468a7e54b6eb38f14`
- Dimensions/import: 1536×1024, single sprite, 100 PPU, alpha enabled
- Referenced By: `Peloponeso.asset` and `Peloponeso_Arte/SpriteRenderer`
- Territory Geometry: 82 PNG masks in `Assets/Mapas/Peloponeso/Mascaras/`, scene
  territory objects, colliders and counters.
- Status: `ACTIVE_CANONICAL`
- Authoring references were moved, with their `.meta`, to
  `Legacy/Assets/Maps/Peloponeso/Reference/`; both had zero Unity consumers.

## Dark World

- Logical Definition: `Assets/Resources/Mapas/DarkWorld.asset`
- Definition GUID: `6754f4f9fd7e3d34f8f62be7745d7fc8`
- Scene: `Assets/Mapas/Dark World/DarkWorld.unity`
- Scene GUID: `eb75e339d9958964fb5a949cb8322ccc`
- Active Background Art: `Assets/Mapas/Dark World/Arte/DarkWorld_Remaster.png`
- Art GUID: `8ab3ee3ef8ada2d4693357b10698ae97`
- Dimensions/import: 1529×1019, single sprite, 100 PPU, alpha enabled
- Referenced By: `DarkWorld.asset` and `DarkWorld_Arte/SpriteRenderer`
- Territory Geometry: 43 regenerated PNG masks in
  `Assets/Mapas/Dark World/Mascaras/`, scene territory objects, colliders and counters.
- Status: `ACTIVE_CANONICAL`
- `DarkWorld.png` was moved to `Legacy/Assets/Maps/DarkWorld/Variants/`; the three
  authoring aids were moved to `Legacy/Assets/Maps/DarkWorld/Reference/`. All keep
  their `.meta`, had zero Unity consumers and are not exact byte duplicates.

## Critical active assets

### Maps

- `Assets/Resources/Mapas/CatalogoMapas.asset`: canonical map catalog and Classic default.
- Five `DefinicaoMapa` assets and five scenes listed above.
- Runtime: `DefinicaoMapa.cs`, `CatalogoMapas.cs`, `MapaAtivo.cs`,
  `ValidadorDefinicaoMapa.cs`, territory/counter components.
- Editor: `WarDominionMapEditorWindow.cs` and
  `WarDominionMapEditorOperations.cs`; named map generators remain shared authoring tools.

### Scenes

- Home: `Assets/Scenes/Home.unity`.
- Classic gameplay: `Assets/WarGame.unity` (historical technical name intentionally retained).
- Other gameplay scenes are listed under each map.

### Core gameplay

- `Assets/Scripts/Core/GameManager.cs` is the match orchestrator.
- `TerritorioClique` is authoritative territory state; `MapaAtivo` supplies the active
  logical definition.

### Weapon system

- Canonical authority: `WDWeaponResolutionPipeline.ResolveRound`.
- Canonical executor: `WDWeaponExecutionCoordinator`.
- Active state/effects: `WDWeaponMatchRuntime`, `WDGameWeaponContext`,
  `WDGameWeaponMutationPort`, `WDRealWeaponEffects`, `WDWeaponPreparationService`.
- Category domain files remain shared active IDs/tables/contracts.

### Match UI and Home

- Match UI: `WarDominionMatchHUD`, `MatchUIPresenter`, `UICompositionRoot` and current
  weapon/result presenters.
- Home: `WarDominionHomeController`, `WDHomeMatchFlowPanel` and current Home panels.

### Catalogs/configuration

- Maps: `Assets/Resources/Mapas/CatalogoMapas.asset`.
- Weapons: `Assets/Resources/UI/Home/WDArmoryCatalogConfig.asset`.
- Random Cards: `Assets/Resources/Cartas/WDRandomCardsConfig.asset`.
- Match flow: `Assets/Resources/UI/Home/WarDominionMatchFlowConfig.asset`.
- Player palette: `Assets/Resources/Config/WDPlayerPaletteConfig.asset`.
- UI theme: `Assets/Resources/UI/WarDominionUITheme.asset`.

## Candidates and duplicates

- No exact duplicate was found among the audited map art files by SHA-256.
- `Fundo.png` (GUID `737e53c4726693748b1a7a291e2b1fc4`) had zero consumers and
  was preserved at `Legacy/Assets/Maps/Shared/Fundo.png` outside Unity import.
- Gabarito/reference/old-remaster files listed per map are visual variants or authoring
  aids and currently unreferenced. Retain until migration provenance is reviewed.
- UI currently under redesign remains functional active/provisional; appearance alone
  is not evidence of legacy status.

## Recommended naming for new imports

Use `WD_Map_<MapId>_<Role>_<Aspect>_vNN.ext`, for example:

- `WD_Map_Classic_Background_16x9_v01.png`
- `WD_Map_PearlHarbor_Background_16x9_v01.png`
- `WD_Map_Riachuelo_TerritoryMask_CuruzuCuatia_v01.png`
- `WD_Map_DarkWorld_AuthoringGuide_16x9_v01.png`

Avoid `test`, `new`, `final`, or repeated extensions in definitive filenames.

## Recommended future map structure

Preserve current GUIDs until a dedicated migration. For new imports, converge toward:

```text
Assets/Mapas/<MapId>/
  Arte/
  Mascaras/
  Data/          # only if definitions later leave Resources safely
  <MapId>.unity
```

Import each new 16:9 background beside the current art first; assign a new GUID, verify
definition and scene references, then recalibrate geometry in a separate reviewed step.

## Auditoria das candidatas WD 16:9 — 2026-09-10

As artes ativas listadas neste registro continuam sendo as autoridades runtime. Nenhuma
candidata foi promovida sem geometria segura.

- Classic: WD `2a356458fc2283749aa4aee4250932bd`, 1920×1080, `BLOCKED_GEOMETRY`.
- Pearl Harbor: WD `3981cd690f96bc74d870b70d182f23ee`, 1920×1080, `BLOCKED_GEOMETRY`.
- Riachuelo: WD `933abf2749eb613459d31ea0d8ded793`, 1920×1080,
  `PARTIAL_NEEDS_MANUAL_GEOMETRY_REVIEW`.
- Peloponeso: WD `f7239c9798a7cfd458f5b266e753e9c5`, 1920×1080,
  `PARTIAL_NEEDS_MANUAL_GEOMETRY_REVIEW`.
- Dark World: WD `3f0ad453916d0eb4795b432fe351c9aa`, 1920×1080, `BLOCKED_GEOMETRY`.

Detalhes e próximos passos: `Docs/WarDominion-Map16x9MigrationReport.md` e
`Docs/MapMigration/`.

Importação final: os cinco backgrounds WD usam textura 1920×1080 e SpriteRect
full-canvas `(0, 0, 1920, 1080)`. GUIDs e IDs dos sub-assets foram preservados. Essa
padronização de import não promove os backgrounds nem altera geometria territorial.
