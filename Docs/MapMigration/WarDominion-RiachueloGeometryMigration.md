# Batalha do Riachuelo — migração geométrica 16:9

- Status: `PARTIAL_NEEDS_MANUAL_GEOMETRY_REVIEW`
- Estrutura preservada: 37 territórios, 6 regiões, 82 conexões.
- Arte ativa preservada: `Assets/Mapas/Batalha do Riachuelo/Arte/Riachuelo.png` (`6e22641b0342a464eb3cc732e765bb9a`).
- Candidata WD validada: `Assets/Mapas/Batalha do Riachuelo/Arte/WD_Map_Riachuelo_Background_16x9_v01.png` (`933abf2749eb613459d31ea0d8ded793`), 1920×1080.
- Resultado da comparação: a composição territorial mudou de forma não uniforme; uma transformação global/afim não preserva as 37 máscaras e hitboxes com segurança.
- Transform global: não aplicado. Correções individuais: 0. Revisão manual: 37 territórios.
- Sprites, máscaras, colliders, contadores, definição e scene: não alterados.
- Backup territorial: desnecessário nesta execução, pois nenhuma geometria foi gravada.
- Próxima ação: usar o Map Editor sobre a candidata WD e validar cada máscara/collider antes de trocar a autoridade visual.
