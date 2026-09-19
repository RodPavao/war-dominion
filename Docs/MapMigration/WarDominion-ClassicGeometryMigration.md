# Classic — migração geométrica 16:9

- Status: `BLOCKED_NEEDS_MANUAL_GEOMETRY_REVIEW`
- Estrutura preservada: 42 territórios, 6 regiões, 80 conexões.
- Arte ativa preservada: `Assets/Mapas/Classic/Arte/ClassicMap.png` (`f328fe402440c014b88800328223fa03`).
- Candidata WD auditada: `Assets/Mapas/Classic/Arte/WD_Map_ClassicMap_Background_16x9_v01.png` (`2a356458fc2283749aa4aee4250932bd`).
- Dimensão confirmada: 1920×1080. O SpriteRect legado 1920×1081 foi corrigido para 1920×1080, preservando GUID e sprite ID.
- Transform, sprites, máscaras, colliders, contadores, definição e scene: não alterados.
- Backup territorial: desnecessário nesta execução, pois nenhuma geometria foi gravada.
- Próxima ação: executar calibração assistida dos 42 territórios; a composição antiga→nova não é uma simples troca de canvas suficientemente segura para promoção automática.
