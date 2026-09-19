# Classic — definitive art migration, 2026-09-15

## Authorities and scope

Runtime scene: `Assets/WarGame.unity`. Definition: `Assets/Resources/Mapas/Classic.asset`.
Old background: `Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png`, GUID `2a356458fc2283749aa4aee4250932bd`, sprite fileID `3801641741836963537`.
New runtime background: `Assets/Mapas/Classic/Arte/WD_Classic_Map_1920x1080_Territories Transparentes.png`, GUID `a364317755254adfa65d5e9e9a1ea5de`, same full-canvas sprite fileID, centered 1920x1080, PPU100, bilinear/clamp, no mipmaps, no compression.

All three supplied files were copied byte for byte into Classic/Arte. Beige is an opaque visual reference, not the runtime background: in the existing draw ordering an opaque background occludes the recolorable overlays. The colored mask is a technical reference/source only; its colors are never gameplay colors.

The 42 existing Runtime16x9 sprite PNGs remain white/alpha, Single, with their existing GUIDs and scene fileIDs. Their visual fills were adjusted because replacing only the background leaves measurable old-border mismatches. No GameObject, transform, collider, canonical boundary, counter, name, region or connection was rebuilt or edited.
For expanded raster crops, custom pivots compensate the padding exactly, preserving the original sprite-local origin and world registration. No arbitrary transform adjustment.

## Raster correspondence

Five repeated solid reference colors yielded 44 significant connected components. Overlap against the original world-registered white sprites established a bijection: 42 existing identities, 44 components; Indonesia and UnitedKingdom retain two components each. This deliberately preserves existing identity assignments rather than renaming territories geographically.
802 transparent pixels lie outside the colored reference's exact solid fills. Only those transparent pixels were assigned from adjacent components; one isolated pixel at (1002,277) was resolved from a component within an eight-pixel neighborhood. Opaque border/ocean pixels were not repainted. All transparent interior pixels have overlay coverage.

| Existing ID | Original fill pixels | Final fill pixels | Original pixels outside reference fill | Parts |
|---|---:|---:|---:|---:|
| Yakutsk | 7163 | 6910 | 525 | 1 |
| Argentina | 7087 | 7668 | 86 | 1 |
| Arabia | 18116 | 17506 | 976 | 1 |
| India | 10072 | 10286 | 187 | 1 |
| Ethiopia | 12951 | 12860 | 350 | 1 |
| Brazil | 16610 | 17017 | 225 | 1 |
| Scandinavia | 5684 | 5394 | 567 | 1 |
| WestUSA | 10559 | 10519 | 282 | 1 |
| SouthAfrica | 9404 | 9398 | 269 | 1 |
| EastAustralia | 7842 | 7514 | 699 | 1 |
| Indonesia | 8272 | 6907 | 1493 | 2 |
| Venezuela | 6304 | 6884 | 80 | 1 |
| Kamchatka | 9137 | 9560 | 481 | 1 |
| Greenland | 10920 | 11568 | 189 | 1 |
| Ural | 13595 | 13652 | 576 | 1 |
| UnitedKingdom | 4448 | 3753 | 739 | 2 |
| Yukon | 6607 | 7043 | 321 | 1 |
| Irkutsk | 5278 | 5633 | 78 | 1 |
| NewGuinea | 3391 | 2842 | 593 | 1 |
| Siberia | 19937 | 20405 | 204 | 1 |
| WestAustralia | 7586 | 7606 | 260 | 1 |
| Algeria | 20398 | 20697 | 418 | 1 |
| Egypt | 6080 | 6184 | 150 | 1 |
| EastUSA | 9489 | 9780 | 207 | 1 |
| Peru | 3632 | 4011 | 77 | 1 |
| Italy | 5926 | 5731 | 483 | 1 |
| Congo | 6421 | 6669 | 115 | 1 |
| Indochina | 4289 | 4027 | 436 | 1 |
| Russia | 23390 | 23273 | 808 | 1 |
| Alaska | 5242 | 5043 | 472 | 1 |
| Japan | 5788 | 5091 | 871 | 1 |
| Alberta | 9931 | 9804 | 546 | 1 |
| Mongolia | 8870 | 9047 | 249 | 1 |
| Ontario | 7217 | 7283 | 236 | 1 |
| Spain | 4011 | 3786 | 512 | 1 |
| Mexico | 4817 | 4482 | 529 | 1 |
| China | 18546 | 18847 | 257 | 1 |
| Afghanistan | 13024 | 13325 | 115 | 1 |
| Iceland | 2934 | 2746 | 335 | 1 |
| Germany | 6082 | 6386 | 212 | 1 |
| Madagascar | 3678 | 3182 | 527 | 1 |
| Quebec | 5487 | 5347 | 346 | 1 |

## Functional preservation

TerritorioVisual still uses PaletaJogadores.ObterCorNeutraAtiva for UsaOverlaySemDono; otherwise ObterCorAtiva(dono). Empty ownership/skin and canonical troop quantities remain unchanged. Full-map overlay alpha remains 1. No gameplay or presentation-system C# was edited.
Comparison with the task-local backup confirms the scene and definition differ only in the background GUID. The territory metas differ only in origin-preserving spritePivot lines; GUIDs/PPU/Single/settings are retained.

Backup and reproducible visual audit: `_LocalBackups/ClassicDefinitiveVisual_20260915` and `_LocalBackups/classic_definitive_visual_audit.py` (outside Assets).

## Validation

Assembly-CSharp and Assembly-CSharp-Editor: successful, zero errors, zero warnings.
Unity focused suites: WDMap16x9PromotionServiceTests 54/54; WDNeutralTerritoryTests 48/48.
Final Run All after coverage refinement: 828/828 Passed, zero failures (Unity Test Runner and TestResults.xml, 2026-09-15 21:34:18Z–21:34:21Z). Test count unchanged.
Real Play through Home, Classic 2x2: definitive transparent background, owned territory recoloring and separate troop numbers verified. Empty/neutral semantics are covered by the 48 neutral-territory regressions; Empty was not manually induced in Play. No names/counter code was changed.

## Task file inventory

Changed: Assets/WarGame.unity and Assets/Resources/Mapas/Classic.asset (background reference only); all 42 Assets/Mapas/Classic/Runtime16x9/<TerritoryId>.png listed above (visual alpha derivatives only).
Changed 27 origin-preserving .png.meta pivots: Afghanistan, Alaska, Alberta, Algeria, Argentina, China, Congo, EastAustralia, EastUSA, Egypt, Ethiopia, Germany, Greenland, India, Irkutsk, Kamchatka, Mongolia, Ontario, Peru, Russia, Scandinavia, Siberia, Spain, Venezuela, WestUSA, Yakutsk, Yukon.
Added: the three source PNG copies and their three metas in Assets/Mapas/Classic/Arte, and this report. Task-local backup/audit script lives outside Assets in _LocalBackups; no C# or tests were edited.

## Legacy

Old background GUID and ClassicMap.png GUID f328fe402440c014b88800328223fa03 have zero serialized references in Assets scenes/definitions/prefabs at audit time. However, the old 16x9 background path is still referenced by WDMap16x9PromotionService and WarDominionMapEditorOperationsTests. The old technical mask path is used by WDMapTerritoryMaskAuditor and WDMapTerritoryBoundaryGenerator. Therefore these tooling assets are NOT safe to quarantine without a separate migration of those tools/tests.
Runtime16x9 GUIDs remain referenced and must not be quarantined. Historical embedded art/thumbnail derivatives were not deleted, moved or cleaned; zero-ref findings above do not assert that all old image pixels have disappeared from historical/preview derivatives.
No asset was deleted or moved. No staging, commit or push.
