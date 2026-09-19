# WAR DOMINION — relatório de limpeza estrutural

Data: 2026-09-11. Escopo: assets de mapas, pares asset/meta, temporários e
variantes históricas. Nenhum background ativo ou candidato WD foi removido.

## Resumo

- Arquivos auditados sob `Assets`: 1.152 arquivos em 66 diretórios.
- `ORPHAN_META`: 0.
- Assets sem `.meta`: 0.
- Pastas Unity sem `.meta`: 0.
- Temporários encontrados nesta execução: 0.
- Movidos para `Legacy/` fora de `Assets`: 12 assets e seus 12 `.meta`.
- Duplicatas binárias detectadas: 1 par protegido, mantido.

## MOVED_TO_LEGACY — REFERENCE_ONLY / GABARITO_ONLY

Todos os itens abaixo tinham zero referência por GUID em scene, prefab, asset,
material, animation, teste ou código. O `.meta` foi movido junto para preservar a
proveniência; fora de `Assets`, o GUID deixa deliberadamente de ser importado.

| Path original | Path final | Classificação | GUID | Consumers | Ação / motivo seguro |
|---|---|---|---|---:|---|
| `Assets/Mapas/Batalha do Riachuelo/Arte/Riachuelo_GabaritoTerritorios.png` | `Legacy/Assets/Maps/Riachuelo/Reference/Riachuelo_GabaritoTerritorios.png` | GABARITO_ONLY | `32fcabe346a5c334dba8c5eca0e95a06` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Batalha do Riachuelo/Arte/Riachuelo_ReferenciaTerritorios.png` | `Legacy/Assets/Maps/Riachuelo/Reference/Riachuelo_ReferenciaTerritorios.png` | REFERENCE_ONLY | `4022aa3fe9bbb4148870438fd6a2856b` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Pearl Harbor/Arte/PearlHarbor_GabaritoTerritorios.png` | `Legacy/Assets/Maps/PearlHarbor/Reference/PearlHarbor_GabaritoTerritorios.png` | GABARITO_ONLY | `3db35195880980447af774f9d0148a3e` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Pearl Harbor/Arte/PearlHarbor_ReferenciaTerritorios.jpg` | `Legacy/Assets/Maps/PearlHarbor/Reference/PearlHarbor_ReferenciaTerritorios.jpg` | REFERENCE_ONLY | `91c05d7d0d5910e44b55050817d7516a` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Peloponeso/Arte/Peloponeso_GabaritoTerritorios.png` | `Legacy/Assets/Maps/Peloponeso/Reference/Peloponeso_GabaritoTerritorios.png` | GABARITO_ONLY | `182ce18f9b153b24299e50a539d1b647` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Peloponeso/Arte/Peloponeso_ReferenciaTerritorios.png` | `Legacy/Assets/Maps/Peloponeso/Reference/Peloponeso_ReferenciaTerritorios.png` | REFERENCE_ONLY | `3318f98c17a04fe438c02f8a764b319e` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Dark World/Arte/DarkWorld_GabaritoTerritorios.png` | `Legacy/Assets/Maps/DarkWorld/Reference/DarkWorld_GabaritoTerritorios.png` | GABARITO_ONLY | `819c83b71f84b9940ba4bf2d33e9ebbb` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Dark World/Arte/DarkWorld_ReferenciaTerritorios.png` | `Legacy/Assets/Maps/DarkWorld/Reference/DarkWorld_ReferenciaTerritorios.png` | REFERENCE_ONLY | `63f2998657acff248b89bb3111058cd2` | 0 | Referência humana preservada fora do import Unity. |
| `Assets/Mapas/Dark World/Arte/DarkWorld_Remaster_Gabarito.png` | `Legacy/Assets/Maps/DarkWorld/Reference/DarkWorld_Remaster_Gabarito.png` | GABARITO_ONLY | `ad3797dca2d4dfa438bb8d4c0fb2fb40` | 0 | Gabarito do Remaster preservado fora do import Unity. |

## MOVED_TO_LEGACY — LEGACY_CONFIRMED / UNREFERENCED_SAFE

| Path original | Path final | Classificação | GUID | Consumers | Ação / motivo seguro |
|---|---|---|---|---:|---|
| `Assets/Mapas/Classic/Arte/ClassicMap_16x9_TEST.png.png` | `Legacy/Assets/Maps/Classic/Variants/ClassicMap_16x9_TEST.png.png` | LEGACY_CONFIRMED | `f330bbadf648eab41ad8ac1cb9f17140` | 0 | Variante de teste substituída pelo WD; referências existentes eram somente documentais. |
| `Assets/Mapas/Dark World/Arte/DarkWorld.png` | `Legacy/Assets/Maps/DarkWorld/Variants/DarkWorld.png` | LEGACY_CONFIRMED | `55ad73920f1d63e4bb5498440f3398f3` | 0 | Versão pré-Remaster; autoridade ativa continua `DarkWorld_Remaster.png`. |
| `Assets/Mapas/Fundo.png` | `Legacy/Assets/Maps/Shared/Fundo.png` | UNREFERENCED_SAFE | `737e53c4726693748b1a7a291e2b1fc4` | 0 | Sem consumer serializado, dinâmico ou por nome encontrado; preservado como histórico. |

## KEPT_ACTIVE

- Cinco backgrounds ativos antigos: ainda consumidos pelas definições/scenes.
- Cinco `WD_Map_*_Background_16x9_v01.png`: `ACTIVE_MIGRATION_CANDIDATE`.
- Todas as máscaras, scenes, definições, colliders, contadores e conexões.
- `CatalogoMapas.asset`, Home, WarGame, TextMesh Pro e o Starter Pack protegido.

## KEPT_UNCERTAIN

Todo asset sem referência YAML direta que possa ser carregado por Resources, código,
nome, ferramenta Editor ou fluxo de import permaneceu intacto. Ausência de GUID isolada
não foi usada como autorização geral para remoção.

## DUPLICATES_REMOVED

Nenhuma. O único hash SHA-256 duplicado encontrado corresponde ao background ativo da
Home e à cópia fonte dentro do `Strategic Warfare UI Starter Pack`. Ambos foram mantidos:
um é ativo e o outro pertence ao diretório explicitamente protegido.

## Contagem final nas pastas de mapas

| Pasta | Arquivos totais | Assets de conteúdo | `.meta` (inclui pastas) |
|---|---:|---:|---:|
| Classic | 5 | 2 | 3 |
| Pearl Harbor | 98 | 48 | 50 |
| Batalha do Riachuelo | 82 | 40 | 42 |
| Peloponeso | 172 | 85 | 87 |
| Dark World | 94 | 46 | 48 |

## Validação

- Após cada lote, os GUIDs movidos permaneceram sem consumidores dentro de `Assets`.
- Assembly-CSharp: 0 erros / 0 warnings.
- Assembly-CSharp-Editor: 0 erros / 0 warnings.
- O log recente do Unity não contém Missing Script, Missing Sprite ou erro de import
  após os movimentos.
- Último Test Runner efetivamente registrado: 702/702. Total esperado no próximo run:
  707 casos após a proteção full-canvas dos cinco backgrounds.
