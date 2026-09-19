# WAR DOMINION — Legacy Migration Report

Audit date: 2026-09-10. Baseline supplied by Unity: 694/694 EditMode.

## 1. Methodology

The audit combined Git status, file inventory, `.meta` GUID extraction, GUID searches in
scenes/assets/prefabs, `Resources.Load` and editor-path searches, scene YAML inspection,
image dimensions, SHA-256 comparison, and static C# consumer searches. No absence of a
text reference was treated as proof of disposability.

## 2. Files analyzed and classification scope

The detailed scope contains 274 primary files/assets; paired `.meta` files are treated
as inseparable identity records rather than counted a second time:

| Classification | Count | Scope |
|---|---:|---|
| `ACTIVE_CANONICAL` | 34 | map definitions/scenes/active art, map catalog/runtime and canonical weapon/runtime authorities |
| `SHARED_ACTIVE` | 220 | 207 territory masks, eight map authoring tools and five shared weapon domain contracts/tables |
| `PURE_LEGACY` | 4 | four modular weapon handler files |
| `UNCERTAIN` | 4 | four legacy-dependent test suites containing still-valid rule coverage |
| `UNREFERENCED_CANDIDATE` | 11 | Fundo plus map reference/gabarito/old visual variants |
| `DUPLICATE_CANDIDATE` | 0 | no byte-identical audited map art |
| `ACTIVE_MIGRATION_CANDIDATE` | 1 | imported Classic 16:9 candidate |

Protected residues are recorded separately and are not reclassified or included in the
274-item scope.

## 3. Active canonical

The map authority chain and exact GUIDs are in
`Docs/WarDominion-ActiveAssetRegistry.md`. Runtime weapon authority remains
`WDWeaponResolutionPipeline.ResolveRound`; execution remains
`WDWeaponExecutionCoordinator`; `GameManager` is the connected orchestrator.

## 4. Shared active

- All 207 separate territory masks for Pearl Harbor (45), Riachuelo (37),
  Peloponeso (82) and Dark World (43).
- Eight map authoring tools: the current editor window and operations layer, five named
  map generators/editors, and `VizinhosEditor` (retained because its editor entry point
  and dynamic catalog lookup remain functional even though it is not the primary editor).
- `WDWeaponSystemDomain.cs`, `WDSupportWeaponDomain.cs`,
  `WDTacticalWeaponDomain.cs`, `WDStrategicWeaponDomain.cs` and
  `WDSpecialWeaponDomain.cs` because canonical code consumes their IDs/tables/contracts.

## 5. Pure legacy

Static consumer analysis confirms these four files implement the modular `Resolve`
path and are not connected to `GameManager`:

- `Assets/Scripts/Armas/WDSupportWeaponHandlers.cs`
- `Assets/Scripts/Armas/WDTacticalWeaponHandlers.cs`
- `Assets/Scripts/Armas/WDStrategicWeaponHandlers.cs`
- `Assets/Scripts/Armas/WDSpecialWeaponHandlers.cs`

They are behaviorally legacy, but were not moved because active EditMode suites still
compile against them and retain rule coverage not yet mapped test-by-test to
`ResolveRound`.

## 6. Uncertain

These suites directly exercise the modular executor and therefore cannot yet leave
`Assets/Editor/Tests` without an explicit coverage matrix:

- `Assets/Editor/Tests/WDSupportWeaponTests.cs`
- `Assets/Editor/Tests/WDTacticalWeaponTests.cs`
- `Assets/Editor/Tests/WDStrategicWeaponTests.cs`
- `Assets/Editor/Tests/WDSpecialWeaponTests.cs`

`WDWeaponSystemTests.cs` also calls the generic `Resolve` API for foundational pipeline
contracts. It remains `SHARED_ACTIVE` until those generic guarantees are represented by
the canonical surface.

## 7. Duplicate candidates

No exact duplicate was found among the 16 audited map art images. Similar filenames are
visual variants, references or gabaritos with different hashes and dimensions. They
must not be deleted based on name alone.

## 8. Orphan/unreferenced candidates

- `Assets/Mapas/Fundo.png` has no serialized consumer found.
- Pearl Harbor, Riachuelo and Peloponeso each have an unreferenced reference image and
  gabarito.
- Dark World has four unreferenced older/reference/gabarito variants.

These are retained because authoring provenance and future 16:9 migration value are not
encoded in runtime references.

## 9. Movements performed

None. No file satisfied all movement gates simultaneously: zero runtime/editor/test
consumers, canonical replacement coverage, and safe removal from Unity compilation.
Consequently no `Legacy/` directory was created merely to leave it empty.

## 10. Movements deliberately not performed

- Four weapon handler files: `PURE_LEGACY`, held until test migration.
- Four category test suites and generic pipeline tests: still compile against legacy API.
- `WDWeaponResolutionPipeline.cs`: contains active `ResolveRound`; moving it would break
  production. Its legacy `Resolve` surface must be isolated, not the file moved.
- Map art variants: unreferenced does not prove obsolete authoring value.
- UI assets/scripts: current provisional UI is functional and under redesign.
- Documentation: ProductDesign and Resolution Audit contain active material; neither is
  purely historical.
- All protected residues named by the task.

## 11. Test coverage

No test was removed, moved or added by this organization pass. Expected active count
remains 694. The supplied Unity baseline remains the authority until rerun; compilation
is required after document creation only as a safety confirmation, not because gameplay
code changed.

## 12. Remaining risks

- The legacy `Resolve` API and `RegisterActionHandler`/`RegisterPhaseHandler` live in the
  same active file as `ResolveRound`, allowing future semantic drift.
- Old category suites can report green for behavior not used by the match runtime.
- Unreferenced authoring art lacks explicit provenance metadata.
- Classic 16:9 geometry is not calibrated; assigning it permanently now would misalign
  territory sprites, colliders and counters.
- `ClassicMap_16x9_TEST.png.png` has a temporary doubled extension and imported size
  1672×941; do not rename or resample it during this audit.

## 13. Recommended next step

1. Build a rule-by-rule matrix from every assertion in the four old category suites to
   an equivalent `ResolveRound` test.
2. Add only missing canonical cases, then rerun the full Unity suite.
3. Remove the legacy category suites from active compilation and verify the explained
   test-count reduction.
4. Remove the four registration/handler files while preserving their Git history under
   `Legacy/Code/Weapons/` only if the producer still wants repository-visible copies.
5. Extract/remove only `Resolve`, `RegisterActionHandler`, `RegisterPhaseHandler` and
   legacy interfaces from `WDWeaponResolutionPipeline.cs` after zero consumers.
6. In a separate map task, import the four external 16:9 files with the naming convention
   in the active registry, then recalibrate each map independently.

## Execução de migração 16:9 — 2026-09-10

- Movimentos de arte/referência/gabarito: nenhum. As novas artes ainda não passaram por
  ativação geométrica completa, portanto as artes antigas continuam ativas e não podem
  ser classificadas como legacy removível.
- Temporários removidos: os arquivos sem `.meta` e sem consumidores
  `WD_Map_Riachuelo_Background_16x9_v01.png~` e
  `WD_Map_Peloponeso_Background_16x9_v01.png~`.
- Handlers e testes legados de armas: permaneceram intactos; nenhuma cobertura foi
  migrada nesta execução. A API canônica `ResolveRound` continua no arquivo ativo.

## Correção final dos imports 16:9 — 2026-09-11

- SpriteRects corrigidos sem recriação de `.meta`: Classic, Pearl Harbor e Dark World,
  de 1920×1081 para 1920×1080.
- Temporários removidos, todos sem `.meta` e sem consumidores:
  `WD_Map_ClassicMap_Background_16x9_v01.png~`,
  `WD_Map_PearlHarbor_Background_16x9_v01.png~` e
  `WD_Map_DarkWorld_Background_16x9_v01.png~`.
- Referências, gabaritos e artes antigas movidos: nenhum. Como nenhum mapa atingiu
  `COMPLETE`, as autoridades antigas continuam necessárias e não foram quarentenadas.

## Limpeza estrutural comprovada — 2026-09-11

- Foram movidos para `Legacy/Assets/Maps/`, sempre com o `.meta`: nove gabaritos ou
  referências humanas, `ClassicMap_16x9_TEST.png.png`, o `DarkWorld.png` pré-Remaster
  e o `Fundo.png` compartilhado sem consumidores.
- Nenhum background ativo antigo foi movido: eles continuam referenciados enquanto a
  recalibração 16:9 não é concluída.
- Nenhum background WD candidato, máscara, scene, definição ou código foi removido.
- Auditoria global: zero `.meta` órfãos, zero assets sem `.meta`, zero pastas Unity sem
  `.meta` e zero temporários restantes.
- Relatório caminho-a-caminho: `Docs/WarDominion-CleanupReport.md`.

## Movement record template for the future quarantine

```text
FROM: <path>
TO: Legacy/<path>
CLASSIFICATION: PURE_LEGACY
WHY SAFE: <zero active consumers and canonical replacement>
REFERENCES BEFORE: <list>
REFERENCES AFTER: zero
TEST COVERAGE: <canonical tests>
```
