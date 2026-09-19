# War Dominion — relatório de migração dos mapas 16:9

## Resultado seguro desta execução

As cinco candidatas WD agora medem 1920×1080. Classic, Pearl Harbor e Dark World possuíam SpriteRects antigos de 1920×1081; os três foram corrigidos para 1920×1080 sem trocar GUID ou sprite ID. Nenhuma candidata foi ativada porque a comparação com as autoridades atuais não oferece uma transformação global suficientemente segura para sprites territoriais, máscaras, colliders e contadores. As autoridades atuais foram preservadas.

| Mapa | WD candidata | Dimensão | T/R/C preservados | Transform/correções | Status |
|---|---|---:|---:|---|---|
| Classic | `WD_Map_ClassicMap_Background_16x9_v01.png` | 1920×1080 | 42/6/80 | nenhuma | BLOCKED |
| Pearl Harbor | `WD_Map_PearlHarbor_Background_16x9_v01.png` | 1920×1080 | 45/5/91 | nenhuma | BLOCKED |
| Riachuelo | `WD_Map_Riachuelo_Background_16x9_v01.png` | 1920×1080 | 37/6/82 | 0; 37 em revisão | PARTIAL |
| Peloponeso | `WD_Map_Peloponeso_Background_16x9_v01.png` | 1920×1080 | 82/4/164 | 0; 82 em revisão | PARTIAL |
| Dark World | `WD_Map_DarkWorld_Background_16x9_v01.png` | 1920×1080 | 43/7/73 | nenhuma | BLOCKED |

## Classic pilot — 2026-09-11

The Classic candidate is import-valid, but its 16:9 composition does not provide a
safe one-step replacement for the 42 old-canvas territory sprites and colliders. A
coarse non-uniform relation (`1.0986257074`, `1.0671232877`, offset
`210.1374293`, `24.1068493` pixels) was measured and intentionally not applied because
there are no authoritative 42 candidate masks for per-border residual measurement.
The existing Map Editor now provides a non-destructive `Migração16x9` overlay for the
manual pass. Classic status advances from `BLOCKED` to `PARTIAL`; active art and all
runtime geometry remain unchanged pending human review.

## Import audit

As cinco candidatas estão importadas como Sprite, 100 PPU e malha Tight. Classic, Pearl Harbor e Dark World mantêm Sprite Mode Multiple, pivot e IDs existentes; somente a altura do rect foi reduzida de 1081 para 1080. Riachuelo e Peloponeso tinham crops herdados de 1672×941 sem consumidores; seus rects foram normalizados para o canvas completo 1920×1080, preservando GUID, sprite ID, internal ID e demais settings.

### Proteção de regressão dos SpriteRects

O teste parametrizado `WdBackgroundSpritesCoverTheFullTextureCanvas` valida as cinco texturas em 1920×1080, exige ao menos um Sprite importado e verifica que cada `Sprite.rect` começa em (0,0) e cobre exatamente toda a textura. A proteção é restrita aos cinco backgrounds WD canônicos, sem impor essa regra a máscaras ou sprites territoriais.

## Integridade e revisão

- Quantidade total preservada: 249 territórios.
- IDs, regiões, membros, bônus e conexões não foram modificados.
- Nenhuma scene, `DefinicaoMapa`, máscara, collider ou contador foi gravado.
- Não há ranking de outliers porque nenhuma transformação insegura foi aplicada.
- Riachuelo e Peloponeso exigem revisão manual completa; os três demais precisam primeiro de fonte 1920×1080 real.
- Thumbnails permanecem apontando para o estado ativo atual até cada migração ser validada.

## Checklist de Play Mode por mapa após futura migração

Abrir a partida; confirmar background WD; revisar bordas; clicar territórios extremos; validar hitboxes; validar expansão centralizada; conferir contadores; testar ataque/movimento e targeting; ativar Regiões; revisar labels; testar conexões especiais; confirmar HUD intacto.
