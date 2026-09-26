# Manifesto final do checkpoint — 26/09/2026

Somente proposta de staging. Nenhum add, reversão, commit, exclusão, alteração funcional ou configuração LFS executada.
Este documento substitui as recomendações pendentes do relatório anterior nos pontos resolvidos abaixo.

## Home: incluir em Git normal

Todos têm meta existente e são referências serializadas de Assets/Scenes/Home.unity para WarDominionHomeController. Necessários à apresentação atual (background, Play, matchmaking e abertura adaptada à proporção).

| Path | Bytes | MiB | Resolução | Campo consumidor |
|---|---:|---:|---|---|
| `Assets/Art/Backgrounds/Home/Home.png` | 10524441 | 10.04 | 3840x2160 | backgroundTexture |
| `Assets/Art/Backgrounds/Play/Play.png` | 10427660 | 9.94 | 3840x2160 | playBackgroundTexture |
| `Assets/Art/Backgrounds/Matchmaking/Matchmaking.png` | 10251781 | 9.78 | 3840x2160 | matchmakingBackgroundTexture |
| `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_4K_3840x2160.png` | 14069413 | 13.42 | 3840x2160 | warDominionOpening16x9Texture |
| `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_Ultrawide_4800x2160.png` | 17433403 | 16.63 | 4800x2160 | warDominionOpeningUltrawideTexture |

Agregado: 62,706,698 bytes = 59.80 MiB. Nenhum chega a 50 MiB. GitHub avisa acima de 50 MiB e bloqueia acima de 100 MiB: https://docs.github.com/en/repositories/working-with-files/managing-large-files/about-large-files-on-github . Sem impedimento de tamanho para Git normal.

## Dez backgrounds: excluir deste checkpoint

Cada PNG e meta foi auditado por GUID, caminho completo e nome de arquivo no código; nenhum consumidor atual foi encontrado. Todos são PNG 3840x2160, não arquivos editáveis de autoria. São arte disponível, mas a origem Figma e o status de implementação futura de cada tela não podem ser inferidos somente do nome. Não afirmar que a tela inteira está ausente: existem módulos/painéis funcionais que não usam estas imagens. Nenhum equivalente binário usado foi demonstrado. Nenhum ignore novo nem exclusão física.

| Path | MiB | GUID/path | Uso futuro/fonte/equivalente | Decisão |
|---|---:|---|---|---|
| `Assets/Art/Backgrounds/Amigos/Amigos.png` | 9.01 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Arsenal/Arsenal.png` | 9.98 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Chat/Chat.png` | 9.97 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Clan/Clan.png` | 10.55 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Configuracoes/Configuracoes.png` | 10.80 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Loja/Loja.png` | 10.56 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Missoes/Missoes.png` | 9.76 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Perfil/Perfil.png` | 10.23 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Ranking/Ranking.png` | 10.68 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |
| `Assets/Art/Backgrounds/Torneios/Torneios.png` | 9.95 | Sem consumidor | Arte disponível; integração Figma futura não comprovada; equivalente usado não demonstrado | Excluir PNG + meta + meta da pasta exclusiva |

## TMP e UnityConnect

- Bebas: classificação B (cache regenerável). Atlas 1024x1024 -> 1x1, 12 glyphs/caracteres -> tabelas vazias. m_AtlasPopulationMode=1 e m_ClearDynamicDataOnBuild=1; fonte fonte TTF 9f66a5952b035224882acc129d2d9195 existe. Consumidores: ContadorTropas e WDArmyCounterView via Resources.Load. Não excluir a fonte: manter versão HEAD; apenas o diff fica fora.
- LiberationSans fallback: classificação B. +780 linhas de pares de ajuste/kerning; m_GetFontFeatures=1, modo dinâmico e limpeza no build ativos. TTF e3265ab4bf004d28a9537516768c1c75 presente. Referenciado por LiberationSans SDF.asset. Manter versão HEAD; novo cache não é requisito do emoji colorido.
- UnityConnect: nenhum inicializador/uso de Unity.Services/UnityConnect localizado nos scripts. Manifest tem ferramentas Editor (collab-proxy, multiplayer.center) e módulo Unity analytics, que não provam dependência funcional do jogo. Recomendar HEAD m_Enabled=0; não incluir ativação sem consumidor. Origem humana/automática não pode ser provada, mas ausência de necessidade funcional sustenta exclusão.

## Settings — classificação e decisão

| Alteração | Classe | Decisão/evidência |
|---|---|---|
| ProjectSettings branding Tonkin Games/War Dominion, splash, orientação landscape | A | Incluir, coerente com apresentação atual |
| Android ID com.wardominion.game, ícone/splash, batching/compressão | B (configuração Android persistente) | Incluir com assets. Perfil Development presente; não é possível certificar estaticamente qual APK foi construído com cada valor |
| InputActions preloaded | A | Incluir; GUID existente no projeto e EditorBuildSettings |
| Tabelas de ícones e campos serializados Android | C | Manter junto de ProjectSettings: configuração persistente coerente, sem limpeza oportunista |
| Quality corrente High e vSync=0 | A | Incluir; WDSettingsData default High e WDUnitySettingsRuntime.ApplyGraphics aplica vSync=0 e SetQualityLevel |
| Quality serializedVersion/meshLodThreshold/plataforma adicional | C | Incluir serialização atual junto de Quality; não é mudança experimental demonstrada |
| UniversalRP campos m_Prefilter* | C | Não incluir; ShaderBuildPreprocessor chama UpdateShaderKeywordPrefiltering e recalcula os campos |
| URP Global m_RuntimeSettings lista | C | Não incluir; RenderPipelineGraphicsSettingsContainer.OnBeforeSerialize limpa e repopula durante build; OnAfterDeserialize limpa no Editor |
| MatchFlowConfig dark_world enabled=0 | A | Incluir: preserva decisão explícita de manter mapa desativado |
| WarGame faseAtual=0 | A | Incluir estado atual; valor de cena funcional, sem evidência que justifique reversão |

Não foi constatada configuração D (experimental) apenas a partir dos diffs. As classes A/B indicam coerência técnica com o estado atual, não nova validação em aparelho.

## Build Profiles e UI Toolkit — individual

- Assets/Settings/Build Profiles/Android - Development.asset: incluir; m_Development=1, Android target=13, sem override de cenas/PlayerSettings. Perfil legítimo persistente, opcional para abrir/compilar, útil para reproduzir build de desenvolvimento.
- Android - Development.asset.meta e Assets/Settings/Build Profiles.meta: incluir para preservar GUIDs/pasta.
- Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss: excluir, somente import unity-theme://default, sem consumidor identificado.
- Seu .meta, UnityThemes.meta e Assets/UI Toolkit.meta: excluir junto; não necessários à UI uGUI atual.

## Dark World

- Excluir os dois relatórios novos WD16x9_dark_world_DryRun/Promotion (documentação exclusiva do mapa congelado).
- Manter mask de Migration e metas: caminho consumido por registros existentes em WDMapMigrationBoundaryStore.asset. Exclusão deixaria dependência de ferramenta de migração sem arquivo; exceção de compatibilidade.
- Manter MapThumbnails/DarkWorld.png e meta: código GetMapThumbnail ainda possui mapeamento Resources para o catálogo. Preserva compatibilidade sem reativar o mapa.
- Manter mudança enabled=0 no MatchFlowConfig: excluí-la publicaria HEAD com mapa habilitado, contradizendo estado congelado informado.
- Não identificada outra alteração rastreada exclusiva de Dark World no diff inspecionado; store contém correções Classic, não remoção do suporte legado.

## Dependências e metas

Verificação de metas dos assets candidatos: 0 ausências. Referências GUID de arquivos do grupo A a novos assets do grupo B: 0. Fontes HEAD e seus TTFs permanecem no repositório. Metas de diretórios exclusivos excluídos acompanham a exclusão; diretórios compartilhados permanecem. Resources dinâmicos do Chat/Home e DarkWorld preservados. Inspeção estática não equivale a um build limpo ou garante dependências construídas arbitrariamente por reflexão.

## GRUPO A — incluir no staging

Lista fechada de paths abaixo. Não usar git add . nem diretórios pais amplos. Não executar automaticamente este documento.

```text
.gitignore
Assets/Art.meta
Assets/Art/Backgrounds.meta
Assets/Art/Backgrounds/Home.meta
Assets/Art/Backgrounds/Home/Home.png
Assets/Art/Backgrounds/Home/Home.png.meta
Assets/Art/Backgrounds/Matchmaking.meta
Assets/Art/Backgrounds/Matchmaking/Matchmaking.png
Assets/Art/Backgrounds/Matchmaking/Matchmaking.png.meta
Assets/Art/Backgrounds/Play.meta
Assets/Art/Backgrounds/Play/Play.png
Assets/Art/Backgrounds/Play/Play.png.meta
Assets/Art/Branding.meta
Assets/Art/Branding/TonkinGames.meta
Assets/Art/Branding/TonkinGames/TonkinGames_Splash_4K_3840x2160.png
Assets/Art/Branding/TonkinGames/TonkinGames_Splash_4K_3840x2160.png.meta
Assets/Art/Branding/WarDominion.meta
Assets/Art/Branding/WarDominion/AppIcon.meta
Assets/Art/Branding/WarDominion/AppIcon/WarDominion_AppIcon_Master.png
Assets/Art/Branding/WarDominion/AppIcon/WarDominion_AppIcon_Master.png.meta
Assets/Art/Branding/WarDominion/Loading.meta
Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_4K_3840x2160.png
Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_4K_3840x2160.png.meta
Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_Ultrawide_4800x2160.png
Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_Ultrawide_4800x2160.png.meta
Assets/Editor/Tests/WDChatEmojiPickerTests.cs
Assets/Editor/Tests/WDChatEmojiPickerTests.cs.meta
Assets/Editor/Tests/WDMap16x9PromotionServiceTests.cs
Assets/Editor/Tests/WDMatchChatViewTests.cs
Assets/Editor/Tests/WDMatchChatViewTests.cs.meta
Assets/Editor/Tests/WDMatchSettingsViewTests.cs
Assets/Editor/Tests/WDMatchSettingsViewTests.cs.meta
Assets/Editor/Tests/WDMatchUIV2Tests.cs
Assets/Editor/Tests/WDMatchUIV2Tests.cs.meta
Assets/Editor/Tests/WDWeaponExecutionCoordinatorTests.cs
Assets/Editor/Tests/WarDominionMapEditorOperationsTests.cs
Assets/Editor/WDMap16x9PromotionApplier.cs
Assets/Editor/WDMapMigrationBoundaryStore.asset
Assets/Editor/WarDominionMapEditorOperations.cs
Assets/Editor/WarDominionMapEditorWindow.cs
Assets/Editor/WarDominionUIEditor.cs
Assets/Mapas/Batalha do Riachuelo/Migration.meta
Assets/Mapas/Batalha do Riachuelo/Migration/WD_Map_Riachuelo_TerritoryMask_16x9_v01.png
Assets/Mapas/Batalha do Riachuelo/Migration/WD_Map_Riachuelo_TerritoryMask_16x9_v01.png.meta
Assets/Mapas/Classic/Migration.meta
Assets/Mapas/Classic/Migration/WD_Map_Classic_TerritoryMask_16x9_v01.png
Assets/Mapas/Classic/Migration/WD_Map_Classic_TerritoryMask_16x9_v01.png.meta
Assets/Mapas/Dark World/Migration.meta
Assets/Mapas/Dark World/Migration/WD_Map_DarkWorld_TerritoryMask_16x9_v01.png
Assets/Mapas/Dark World/Migration/WD_Map_DarkWorld_TerritoryMask_16x9_v01.png.meta
Assets/Mapas/Pearl Harbor/Migration.meta
Assets/Mapas/Pearl Harbor/Migration/WD_Map_PearlHarbor_TerritoryMask_16x9_v01.png
Assets/Mapas/Pearl Harbor/Migration/WD_Map_PearlHarbor_TerritoryMask_16x9_v01.png.meta
Assets/Mapas/Peloponeso/Migration.meta
Assets/Mapas/Peloponeso/Migration/WD_Map_Peloponeso_TerritoryMask_16x9_v01.png
Assets/Mapas/Peloponeso/Migration/WD_Map_Peloponeso_TerritoryMask_16x9_v01.png.meta
Assets/Resources/Mapas/Classic.asset
Assets/Resources/Mapas/Peloponeso.asset
Assets/Resources/Mapas/Riachuelo.asset
Assets/Resources/UI/Chat.meta
Assets/Resources/UI/Chat/Emoji.meta
Assets/Resources/UI/Chat/Emoji/NotoColor.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/AUTHORS.txt
Assets/Resources/UI/Chat/Emoji/NotoColor/AUTHORS.txt.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/Apache-2.0.txt
Assets/Resources/UI/Chat/Emoji/NotoColor/Apache-2.0.txt.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/Atlas.png
Assets/Resources/UI/Chat/Emoji/NotoColor/Atlas.png.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/ChatNotoColor.asset
Assets/Resources/UI/Chat/Emoji/NotoColor/ChatNotoColor.asset.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/ColorSprite.mat
Assets/Resources/UI/Chat/Emoji/NotoColor/ColorSprite.mat.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/LICENSE.txt
Assets/Resources/UI/Chat/Emoji/NotoColor/LICENSE.txt.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/NOTICE.txt
Assets/Resources/UI/Chat/Emoji/NotoColor/NOTICE.txt.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f3ae.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f3ae.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f3c6.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f3c6.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44a.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44a.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44c.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44c.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44d.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44d.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44e.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44e.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44f.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f44f.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f499.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f499.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f4aa.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f4aa.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f4af.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f4af.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f525.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f525.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f600.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f600.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f601.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f601.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f602.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f602.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f603.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f603.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f604.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f604.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f60a.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f60a.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f60d.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f60d.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f60e.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f60e.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f610.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f610.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f621.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f621.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f622.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f622.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f62d.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f62d.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f64c.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f64c.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f914.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f914.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f91d.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f91d.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f923.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f923.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f92f.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u1f92f.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u26a1.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u26a1.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u2705.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u2705.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u270c.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u270c.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u274c.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u274c.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u2764.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u2764.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u2b50.svg
Assets/Resources/UI/Chat/Emoji/NotoColor/emoji_u2b50.svg.meta
Assets/Resources/UI/Chat/Emoji/NotoColor/manifest.json
Assets/Resources/UI/Chat/Emoji/NotoColor/manifest.json.meta
Assets/Resources/UI/ChatEmoji.meta
Assets/Resources/UI/ChatEmoji/NotoEmoji-Regular.ttf
Assets/Resources/UI/ChatEmoji/NotoEmoji-Regular.ttf.meta
Assets/Resources/UI/ChatEmoji/OFL.txt
Assets/Resources/UI/ChatEmoji/OFL.txt.meta
Assets/Resources/UI/ChatEmoji/Smiley.png
Assets/Resources/UI/ChatEmoji/Smiley.png.meta
Assets/Resources/UI/ChatEmoji/Smiley.svg
Assets/Resources/UI/ChatEmoji/Smiley.svg.meta
Assets/Resources/UI/Fonts.meta
Assets/Resources/UI/Fonts/Gabarito-Black.ttf
Assets/Resources/UI/Fonts/Gabarito-Black.ttf.meta
Assets/Resources/UI/Fonts/Gabarito-Bold.ttf
Assets/Resources/UI/Fonts/Gabarito-Bold.ttf.meta
Assets/Resources/UI/Fonts/Gabarito-ExtraBold.ttf
Assets/Resources/UI/Fonts/Gabarito-ExtraBold.ttf.meta
Assets/Resources/UI/Fonts/Gabarito-Medium.ttf
Assets/Resources/UI/Fonts/Gabarito-Medium.ttf.meta
Assets/Resources/UI/Fonts/Gabarito-OFL.txt
Assets/Resources/UI/Fonts/Gabarito-OFL.txt.meta
Assets/Resources/UI/Fonts/Gabarito-SemiBold.ttf
Assets/Resources/UI/Fonts/Gabarito-SemiBold.ttf.meta
Assets/Resources/UI/Fonts/Geist-Medium.ttf
Assets/Resources/UI/Fonts/Geist-Medium.ttf.meta
Assets/Resources/UI/Fonts/Geist-OFL.txt
Assets/Resources/UI/Fonts/Geist-OFL.txt.meta
Assets/Resources/UI/Fonts/Inter-Bold.otf
Assets/Resources/UI/Fonts/Inter-Bold.otf.meta
Assets/Resources/UI/Fonts/Inter-Regular.otf
Assets/Resources/UI/Fonts/Inter-Regular.otf.meta
Assets/Resources/UI/Fonts/Inter-SemiBold.otf
Assets/Resources/UI/Fonts/Inter-SemiBold.otf.meta
Assets/Resources/UI/Home/FigmaPhase1A.meta
Assets/Resources/UI/Home/FigmaPhase1A/Bell.png
Assets/Resources/UI/Home/FigmaPhase1A/Bell.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Briefcase.png
Assets/Resources/UI/Home/FigmaPhase1A/Briefcase.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Canonical.meta
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/DivisionEmblem.png
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/DivisionEmblem.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/FriendsGold.png
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/FriendsGold.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/ProfileUser.png
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/ProfileUser.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/ProfileUserDecorative.png
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/ProfileUserDecorative.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/SettingsGold.png
Assets/Resources/UI/Home/FigmaPhase1A/Canonical/SettingsGold.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Chat.png
Assets/Resources/UI/Home/FigmaPhase1A/Chat.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Chevron.png
Assets/Resources/UI/Home/FigmaPhase1A/Chevron.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Coin.png
Assets/Resources/UI/Home/FigmaPhase1A/Coin.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/CrossedSwords.png
Assets/Resources/UI/Home/FigmaPhase1A/CrossedSwords.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/CrossedSwordsGlyph.png
Assets/Resources/UI/Home/FigmaPhase1A/CrossedSwordsGlyph.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Flag.png
Assets/Resources/UI/Home/FigmaPhase1A/Flag.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Gem.png
Assets/Resources/UI/Home/FigmaPhase1A/Gem.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Handshake.png
Assets/Resources/UI/Home/FigmaPhase1A/Handshake.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/HomeAvatar.png
Assets/Resources/UI/Home/FigmaPhase1A/HomeAvatar.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/HomeBackdrop.png
Assets/Resources/UI/Home/FigmaPhase1A/HomeBackdrop.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/HomeBackdrop1B.png
Assets/Resources/UI/Home/FigmaPhase1A/HomeBackdrop1B.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/HomeEmblem.png
Assets/Resources/UI/Home/FigmaPhase1A/HomeEmblem.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/PlayDeckLock.png
Assets/Resources/UI/Home/FigmaPhase1A/PlayDeckLock.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/PlayModeLock.png
Assets/Resources/UI/Home/FigmaPhase1A/PlayModeLock.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Settings.png
Assets/Resources/UI/Home/FigmaPhase1A/Settings.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Shield.png
Assets/Resources/UI/Home/FigmaPhase1A/Shield.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/ShoppingCart.png
Assets/Resources/UI/Home/FigmaPhase1A/ShoppingCart.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/TournamentTrophy.png
Assets/Resources/UI/Home/FigmaPhase1A/TournamentTrophy.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Trophy.png
Assets/Resources/UI/Home/FigmaPhase1A/Trophy.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/User.png
Assets/Resources/UI/Home/FigmaPhase1A/User.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/UserPlayNeutral.png
Assets/Resources/UI/Home/FigmaPhase1A/UserPlayNeutral.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/Users.png
Assets/Resources/UI/Home/FigmaPhase1A/Users.png.meta
Assets/Resources/UI/Home/FigmaPhase1A/UsersPlayNeutral.png
Assets/Resources/UI/Home/FigmaPhase1A/UsersPlayNeutral.png.meta
Assets/Resources/UI/Home/MapThumbnails.meta
Assets/Resources/UI/Home/MapThumbnails/Classic.png
Assets/Resources/UI/Home/MapThumbnails/Classic.png.meta
Assets/Resources/UI/Home/MapThumbnails/DarkWorld.png
Assets/Resources/UI/Home/MapThumbnails/DarkWorld.png.meta
Assets/Resources/UI/Home/MapThumbnails/PearlHarbor.png
Assets/Resources/UI/Home/MapThumbnails/PearlHarbor.png.meta
Assets/Resources/UI/Home/MapThumbnails/Peloponeso.png
Assets/Resources/UI/Home/MapThumbnails/Peloponeso.png.meta
Assets/Resources/UI/Home/MapThumbnails/Riachuelo.png
Assets/Resources/UI/Home/MapThumbnails/Riachuelo.png.meta
Assets/Resources/UI/Home/PlayFinal.meta
Assets/Resources/UI/Home/PlayFinal/FfaTarget.png
Assets/Resources/UI/Home/PlayFinal/FfaTarget.png.meta
Assets/Resources/UI/Home/PlayFinal/FriendliesGroupInvite.png
Assets/Resources/UI/Home/PlayFinal/FriendliesGroupInvite.png.meta
Assets/Resources/UI/Home/PlayFinal/OneVsOneSwords.png
Assets/Resources/UI/Home/PlayFinal/OneVsOneSwords.png.meta
Assets/Resources/UI/Home/PlayFinal/PlayCrossedSwords.png
Assets/Resources/UI/Home/PlayFinal/PlayCrossedSwords.png.meta
Assets/Resources/UI/Home/PlayFinal/ThreeVsThreeHelmet.png
Assets/Resources/UI/Home/PlayFinal/ThreeVsThreeHelmet.png.meta
Assets/Resources/UI/Home/PlayFinal/ThreeVsThreeLock.png
Assets/Resources/UI/Home/PlayFinal/ThreeVsThreeLock.png.meta
Assets/Resources/UI/Home/PlayFinal/TournamentTrophy.png
Assets/Resources/UI/Home/PlayFinal/TournamentTrophy.png.meta
Assets/Resources/UI/Home/PlayFinal/TwoVsTwo.png
Assets/Resources/UI/Home/PlayFinal/TwoVsTwo.png.meta
Assets/Resources/UI/Home/WarDominionMatchFlowConfig.asset
Assets/Resources/UI/Match/Chat.meta
Assets/Resources/UI/Match/Chat/ArrowRight.png
Assets/Resources/UI/Match/Chat/ArrowRight.png.meta
Assets/Resources/UI/Match/Chat/ArrowRight.svg
Assets/Resources/UI/Match/Chat/ArrowRight.svg.meta
Assets/Resources/UI/Match/Chat/Clan.png
Assets/Resources/UI/Match/Chat/Clan.png.meta
Assets/Resources/UI/Match/Chat/Clan.svg
Assets/Resources/UI/Match/Chat/Clan.svg.meta
Assets/Resources/UI/Match/Chat/Discord.png
Assets/Resources/UI/Match/Chat/Discord.png.meta
Assets/Resources/UI/Match/Chat/External.png
Assets/Resources/UI/Match/Chat/External.png.meta
Assets/Resources/UI/Match/Chat/External.svg
Assets/Resources/UI/Match/Chat/External.svg.meta
Assets/Resources/UI/Match/Chat/Friends.png
Assets/Resources/UI/Match/Chat/Friends.png.meta
Assets/Resources/UI/Match/Chat/Friends.svg
Assets/Resources/UI/Match/Chat/Friends.svg.meta
Assets/Resources/UI/Match/Chat/Globe.png
Assets/Resources/UI/Match/Chat/Globe.png.meta
Assets/Resources/UI/Match/Chat/Globe.svg
Assets/Resources/UI/Match/Chat/Globe.svg.meta
Assets/Resources/UI/Match/Chat/HexCyan.png
Assets/Resources/UI/Match/Chat/HexCyan.png.meta
Assets/Resources/UI/Match/Chat/HexCyan.svg
Assets/Resources/UI/Match/Chat/HexCyan.svg.meta
Assets/Resources/UI/Match/Chat/HexNeutral.png
Assets/Resources/UI/Match/Chat/HexNeutral.png.meta
Assets/Resources/UI/Match/Chat/HexNeutral.svg
Assets/Resources/UI/Match/Chat/HexNeutral.svg.meta
Assets/Resources/UI/Match/Chat/Options.png
Assets/Resources/UI/Match/Chat/Options.png.meta
Assets/Resources/UI/Match/Chat/Options.svg
Assets/Resources/UI/Match/Chat/Options.svg.meta
Assets/Resources/UI/Match/Chat/Plus.png
Assets/Resources/UI/Match/Chat/Plus.png.meta
Assets/Resources/UI/Match/Chat/Plus.svg
Assets/Resources/UI/Match/Chat/Plus.svg.meta
Assets/Resources/UI/Match/Chat/Send.png
Assets/Resources/UI/Match/Chat/Send.png.meta
Assets/Resources/UI/Match/Chat/Send.svg
Assets/Resources/UI/Match/Chat/Send.svg.meta
Assets/Resources/UI/Match/Chat/Support.png
Assets/Resources/UI/Match/Chat/Support.png.meta
Assets/Resources/UI/Match/Chat/Support.svg
Assets/Resources/UI/Match/Chat/Support.svg.meta
Assets/Resources/UI/Match/Figma.meta
Assets/Resources/UI/Match/Figma/Chat.png
Assets/Resources/UI/Match/Figma/Chat.png.meta
Assets/Resources/UI/Match/Figma/Installations.meta
Assets/Resources/UI/Match/Figma/Installations/Artillery.png
Assets/Resources/UI/Match/Figma/Installations/Artillery.png.meta
Assets/Resources/UI/Match/Figma/Installations/Bunker.png
Assets/Resources/UI/Match/Figma/Installations/Bunker.png.meta
Assets/Resources/UI/Match/Figma/Installations/Drone.png
Assets/Resources/UI/Match/Figma/Installations/Drone.png.meta
Assets/Resources/UI/Match/Figma/Installations/FlakTower.png
Assets/Resources/UI/Match/Figma/Installations/FlakTower.png.meta
Assets/Resources/UI/Match/Figma/Installations/Minefield.png
Assets/Resources/UI/Match/Figma/Installations/Minefield.png.meta
Assets/Resources/UI/Match/Figma/Installations/PowerPlant.png
Assets/Resources/UI/Match/Figma/Installations/PowerPlant.png.meta
Assets/Resources/UI/Match/Figma/Installations/Rockets.png
Assets/Resources/UI/Match/Figma/Installations/Rockets.png.meta
Assets/Resources/UI/Match/Figma/Menu.png
Assets/Resources/UI/Match/Figma/Menu.png.meta
Assets/Resources/UI/Match/Figma/PlayerAvatar.png
Assets/Resources/UI/Match/Figma/PlayerAvatar.png.meta
Assets/Resources/UI/Match/Figma/RadialSegment.png
Assets/Resources/UI/Match/Figma/RadialSegment.png.meta
Assets/Resources/UI/Match/Figma/Regions.png
Assets/Resources/UI/Match/Figma/Regions.png.meta
Assets/Resources/UI/Match/Figma/Settings.png
Assets/Resources/UI/Match/Figma/Settings.png.meta
Assets/Resources/UI/Match/FigmaCurrent.meta
Assets/Resources/UI/Match/FigmaCurrent/Chat.png
Assets/Resources/UI/Match/FigmaCurrent/Chat.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Chat.svg
Assets/Resources/UI/Match/FigmaCurrent/Chat.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Chevron.png
Assets/Resources/UI/Match/FigmaCurrent/Chevron.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Chevron.svg
Assets/Resources/UI/Match/FigmaCurrent/Chevron.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Energy.png
Assets/Resources/UI/Match/FigmaCurrent/Energy.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Energy.svg
Assets/Resources/UI/Match/FigmaCurrent/Energy.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji1.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji1.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji1.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji1.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji2.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji2.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji2.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji2.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji3.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji3.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji3.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji3.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji4.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji4.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji4.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji4.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji5.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji5.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji5.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji5.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji6.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji6.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji6.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji6.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji7.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji7.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji7.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/Emoji7.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment1.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment1.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment1.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment1.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment2.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment2.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment2.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment2.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment3.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment3.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment3.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment3.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment4.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment4.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment4.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment4.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment5.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment5.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment5.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment5.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment6.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment6.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment6.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment6.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment7.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment7.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment7.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/EmojiSegment7.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/HubAvatar.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/HubAvatar.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/HubAvatar.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/HubAvatar.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment1.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment1.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment1.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment1.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment2.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment2.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment2.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment2.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment3.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment3.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment3.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment3.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment4.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment4.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment4.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment4.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment5.png
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment5.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment5.svg
Assets/Resources/UI/Match/FigmaCurrent/Hubs/TacticalSegment5.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Menu.png
Assets/Resources/UI/Match/FigmaCurrent/Menu.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Menu.svg
Assets/Resources/UI/Match/FigmaCurrent/Menu.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Player1.png
Assets/Resources/UI/Match/FigmaCurrent/Player1.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Player1.svg
Assets/Resources/UI/Match/FigmaCurrent/Player1.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Player2.png
Assets/Resources/UI/Match/FigmaCurrent/Player2.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Player2.svg
Assets/Resources/UI/Match/FigmaCurrent/Player2.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Player3.png
Assets/Resources/UI/Match/FigmaCurrent/Player3.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Player3.svg
Assets/Resources/UI/Match/FigmaCurrent/Player3.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Player4.png
Assets/Resources/UI/Match/FigmaCurrent/Player4.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Player4.svg
Assets/Resources/UI/Match/FigmaCurrent/Player4.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Regions.png
Assets/Resources/UI/Match/FigmaCurrent/Regions.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Regions.svg
Assets/Resources/UI/Match/FigmaCurrent/Regions.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Settings.png
Assets/Resources/UI/Match/FigmaCurrent/Settings.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Settings.svg
Assets/Resources/UI/Match/FigmaCurrent/Settings.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Time.png
Assets/Resources/UI/Match/FigmaCurrent/Time.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Time.svg
Assets/Resources/UI/Match/FigmaCurrent/Time.svg.meta
Assets/Resources/UI/Match/FigmaCurrent/Troops.png
Assets/Resources/UI/Match/FigmaCurrent/Troops.png.meta
Assets/Resources/UI/Match/FigmaCurrent/Troops.svg
Assets/Resources/UI/Match/FigmaCurrent/Troops.svg.meta
Assets/Resources/UI/Match/Official.meta
Assets/Resources/UI/Match/Official/ChatNeutral.png
Assets/Resources/UI/Match/Official/ChatNeutral.png.meta
Assets/Resources/UI/Match/Official/ChatNeutralSource.svg
Assets/Resources/UI/Match/Official/ChatNeutralSource.svg.meta
Assets/Resources/UI/Match/Official/Energy.png
Assets/Resources/UI/Match/Official/Energy.png.meta
Assets/Resources/UI/Match/Official/Energy.svg
Assets/Resources/UI/Match/Official/Energy.svg.meta
Assets/Resources/UI/Match/Official/Movements.png
Assets/Resources/UI/Match/Official/Movements.png.meta
Assets/Resources/UI/Match/Official/Movements.svg
Assets/Resources/UI/Match/Official/Movements.svg.meta
Assets/Resources/UI/Match/Official/PlayerMarkerMask.png
Assets/Resources/UI/Match/Official/PlayerMarkerMask.png.meta
Assets/Resources/UI/Match/Official/PlayerStatEnergy.png
Assets/Resources/UI/Match/Official/PlayerStatEnergy.png.meta
Assets/Resources/UI/Match/Official/PlayerStatTerritories.png
Assets/Resources/UI/Match/Official/PlayerStatTerritories.png.meta
Assets/Resources/UI/Match/Official/PlayerStatTroops.png
Assets/Resources/UI/Match/Official/PlayerStatTroops.png.meta
Assets/Resources/UI/Match/Official/SettingsNeutral.png
Assets/Resources/UI/Match/Official/SettingsNeutral.png.meta
Assets/Resources/UI/Match/Official/SettingsNeutralSource.svg
Assets/Resources/UI/Match/Official/SettingsNeutralSource.svg.meta
Assets/Resources/UI/Match/Official/Speed.svg
Assets/Resources/UI/Match/Official/Speed.svg.meta
Assets/Resources/UI/Match/Official/SpeedGold.png
Assets/Resources/UI/Match/Official/SpeedGold.png.meta
Assets/Resources/UI/Match/Official/SpeedGold.svg
Assets/Resources/UI/Match/Official/SpeedGold.svg.meta
Assets/Resources/UI/Match/Official/SpeedOff.png
Assets/Resources/UI/Match/Official/SpeedOff.png.meta
Assets/Resources/UI/Match/Official/SpeedOffSource.svg
Assets/Resources/UI/Match/Official/SpeedOffSource.svg.meta
Assets/Resources/UI/Match/Official/SpeedOn.png
Assets/Resources/UI/Match/Official/SpeedOn.png.meta
Assets/Resources/UI/Match/Official/SpeedOnSource.svg
Assets/Resources/UI/Match/Official/SpeedOnSource.svg.meta
Assets/Resources/UI/Match/Official/Time.png
Assets/Resources/UI/Match/Official/Time.png.meta
Assets/Resources/UI/Match/Official/Time.svg
Assets/Resources/UI/Match/Official/Time.svg.meta
Assets/Resources/UI/Match/Official/Troops.png
Assets/Resources/UI/Match/Official/Troops.png.meta
Assets/Resources/UI/Match/Official/TroopsSource.svg
Assets/Resources/UI/Match/Official/TroopsSource.svg.meta
Assets/Resources/UI/Match/Settings.meta
Assets/Resources/UI/Match/Settings/LeaveGlow.png
Assets/Resources/UI/Match/Settings/LeaveGlow.png.meta
Assets/Resources/UI/Match/Settings/SliderHandle.png
Assets/Resources/UI/Match/Settings/SliderHandle.png.meta
Assets/Resources/UI/Match/Settings/SliderHandle.svg
Assets/Resources/UI/Match/Settings/SliderHandle.svg.meta
Assets/Resources/UI/Match/Settings/Volume.png
Assets/Resources/UI/Match/Settings/Volume.png.meta
Assets/Resources/UI/Match/Settings/Volume.svg
Assets/Resources/UI/Match/Settings/Volume.svg.meta
Assets/Scenes/Home.unity
Assets/Scripts/Config/WDRegionalSettingsConfig.cs
Assets/Scripts/Core/GameManager.cs
Assets/Scripts/Core/GerenciadorRodada.cs
Assets/Scripts/Mapa/DefinicaoMapa.cs
Assets/Scripts/Mapa/MapaAtivo.cs
Assets/Scripts/Mapa/ValidadorDefinicaoMapa.cs
Assets/Scripts/Territorios/TerritorioContador.cs
Assets/Scripts/UI/Home/WDChatDomain.cs
Assets/Scripts/UI/Home/WDChatHubPanel.cs
Assets/Scripts/UI/Home/WDHomeComponents.cs
Assets/Scripts/UI/Home/WDHomeMatchFlowPanel.cs
Assets/Scripts/UI/Home/WDHomeNotificationCenterPanel.cs
Assets/Scripts/UI/Home/WDHomeNotificationCenterPanel.cs.meta
Assets/Scripts/UI/Home/WarDominionHomeController.cs
Assets/Scripts/UI/LayoutPartida.cs
Assets/Scripts/UI/MatchUICommands.cs
Assets/Scripts/UI/MatchUIPresenter.cs
Assets/Scripts/UI/MatchUIState.cs
Assets/Scripts/UI/ResolutionSequenceController.cs
Assets/Scripts/UI/UICompositionRoot.cs
Assets/Scripts/UI/WDChatEmojiColor.cs
Assets/Scripts/UI/WDChatEmojiColor.cs.meta
Assets/Scripts/UI/WDChatEmojiPicker.cs
Assets/Scripts/UI/WDChatEmojiPicker.cs.meta
Assets/Scripts/UI/WDChatEmojiText.cs
Assets/Scripts/UI/WDChatEmojiText.cs.meta
Assets/Scripts/UI/WDChatEmojiToggleFilter.cs
Assets/Scripts/UI/WDChatEmojiToggleFilter.cs.meta
Assets/Scripts/UI/WDMatchChatBackdropFilter.cs
Assets/Scripts/UI/WDMatchChatBackdropFilter.cs.meta
Assets/Scripts/UI/WDMatchChatView.cs
Assets/Scripts/UI/WDMatchChatView.cs.meta
Assets/Scripts/UI/WDMatchInformationalOverlayController.cs
Assets/Scripts/UI/WDMatchPlayersContrast.cs
Assets/Scripts/UI/WDMatchPlayersContrast.cs.meta
Assets/Scripts/UI/WDMatchResultView.cs
Assets/Scripts/UI/WDMatchSettingsRuntime.cs
Assets/Scripts/UI/WDMatchSettingsRuntime.cs.meta
Assets/Scripts/UI/WDMatchSettingsView.cs
Assets/Scripts/UI/WDMatchSettingsView.cs.meta
Assets/Scripts/UI/WDMatchUIPresentationBinding.cs
Assets/Scripts/UI/WDMatchUIPresentationBinding.cs.meta
Assets/Scripts/UI/WDMatchUIV2.cs
Assets/Scripts/UI/WDMatchUIV2.cs.meta
Assets/Scripts/UI/WDUIPremiumComponents.cs
Assets/Scripts/UI/WDWeaponInstallationMapPresenter.cs
Assets/Scripts/UI/WDWeaponMatchUIController.cs
Assets/Scripts/UI/WarDominionMatchHUD.cs
Assets/Settings/Build Profiles.meta
Assets/Settings/Build Profiles/Android - Development.asset
Assets/Settings/Build Profiles/Android - Development.asset.meta
Assets/WarGame.unity
Docs/Diagnostics/CheckpointPreparation_2026-09-26.md
Docs/Diagnostics/WD16x9_Classic_DryRun.md
Docs/Diagnostics/WD16x9_Classic_Promotion.md
Docs/Diagnostics/WD16x9_pearl_harbor_DryRun.md
Docs/Diagnostics/WD16x9_pearl_harbor_Promotion.md
Docs/Diagnostics/WD16x9_peloponeso_DryRun.md
Docs/Diagnostics/WD16x9_peloponeso_Promotion.md
Docs/Diagnostics/WD16x9_riachuelo_DryRun.md
Docs/Diagnostics/WD16x9_riachuelo_Promotion.md
Docs/Diagnostics/WDChatEmojiColor_Migration.md
Docs/Diagnostics/WDChatEmojiPicker_Implementation.md
Docs/Diagnostics/WDMatchChat_Implementation.md
Docs/Diagnostics/WDMatchSettings_Implementation.md
Docs/Diagnostics/generate_chat_noto_sprite.py
ProjectSettings/ProjectSettings.asset
ProjectSettings/QualitySettings.asset
Docs/Diagnostics/CheckpointFinalManifest_2026-09-26.md
```

## GRUPO B — não incluir neste checkpoint

```text
Assets/Art/Backgrounds/Amigos.meta
Assets/Art/Backgrounds/Amigos/Amigos.png
Assets/Art/Backgrounds/Amigos/Amigos.png.meta
Assets/Art/Backgrounds/Arsenal.meta
Assets/Art/Backgrounds/Arsenal/Arsenal.png
Assets/Art/Backgrounds/Arsenal/Arsenal.png.meta
Assets/Art/Backgrounds/Chat.meta
Assets/Art/Backgrounds/Chat/Chat.png
Assets/Art/Backgrounds/Chat/Chat.png.meta
Assets/Art/Backgrounds/Clan.meta
Assets/Art/Backgrounds/Clan/Clan.png
Assets/Art/Backgrounds/Clan/Clan.png.meta
Assets/Art/Backgrounds/Configuracoes.meta
Assets/Art/Backgrounds/Configuracoes/Configuracoes.png
Assets/Art/Backgrounds/Configuracoes/Configuracoes.png.meta
Assets/Art/Backgrounds/Loja.meta
Assets/Art/Backgrounds/Loja/Loja.png
Assets/Art/Backgrounds/Loja/Loja.png.meta
Assets/Art/Backgrounds/Missoes.meta
Assets/Art/Backgrounds/Missoes/Missoes.png
Assets/Art/Backgrounds/Missoes/Missoes.png.meta
Assets/Art/Backgrounds/Perfil.meta
Assets/Art/Backgrounds/Perfil/Perfil.png
Assets/Art/Backgrounds/Perfil/Perfil.png.meta
Assets/Art/Backgrounds/Ranking.meta
Assets/Art/Backgrounds/Ranking/Ranking.png
Assets/Art/Backgrounds/Ranking/Ranking.png.meta
Assets/Art/Backgrounds/Torneios.meta
Assets/Art/Backgrounds/Torneios/Torneios.png
Assets/Art/Backgrounds/Torneios/Torneios.png.meta
Assets/Strategic Warfare UI Starter Pack.meta
Assets/Strategic Warfare UI Starter Pack/Scenes.meta
Assets/Strategic Warfare UI Starter Pack/Scenes/FunctionalUI-Demo.unity
Assets/Strategic Warfare UI Starter Pack/Scenes/FunctionalUI-Demo.unity.meta
Assets/Strategic Warfare UI Starter Pack/Scenes/UI-Demo.unity
Assets/Strategic Warfare UI Starter Pack/Scenes/UI-Demo.unity.meta
Assets/Strategic Warfare UI Starter Pack/Strategic Warfare Sci-fi UI Starter Pack Documentation.pdf
Assets/Strategic Warfare UI Starter Pack/Strategic Warfare Sci-fi UI Starter Pack Documentation.pdf.meta
Assets/Strategic Warfare UI Starter Pack/UI Kit.meta
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png.meta
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/HUF Pack.png
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/HUF Pack.png.meta
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/eb07ca70-6ff9-4083-b41f-c81bcc02f133.png
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-png/eb07ca70-6ff9-4083-b41f-c81bcc02f133.png.meta
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-psd.meta
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-psd/UI Starter Pack.psd
Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-psd/UI Starter Pack.psd.meta
Assets/Strategic Warfare UI Starter Pack/prefabs.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Cancel.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Cancel.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Confirm.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Confirm.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Danger.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Danger.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Disabled.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Disabled.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Icon.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Icon.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Primary.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Primary.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Secondary.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/Secondary.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/TextIcon.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Buttons/TextIcon.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/CommandDialogPanel.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/CommandDialogPanel.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents/Command Dialog.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents/Command Dialog.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents/TacticalPaneItem.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents/TacticalPaneItem.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents/VehicleDialog.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/PanelComponents/VehicleDialog.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/TacticalPanel.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/TacticalPanel.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/VehicleDialogPanel.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Panel/VehicleDialogPanel.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/Intel.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/Intel.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/MissionUpdate.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/MissionUpdate.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/SystemWarning.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/SystemWarning.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/Warning.prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/Popups/Warning.prefab.meta
Assets/Strategic Warfare UI Starter Pack/prefabs/StrategicWarfareSci-fi UI (1).prefab
Assets/Strategic Warfare UI Starter Pack/prefabs/StrategicWarfareSci-fi UI (1).prefab.meta
Assets/Strategic Warfare UI Starter Pack/scripts.meta
Assets/Strategic Warfare UI Starter Pack/scripts/ProfileProgressBarUI.cs
Assets/Strategic Warfare UI Starter Pack/scripts/ProfileProgressBarUI.cs.meta
Assets/Strategic Warfare UI Starter Pack/scripts/ProfileScrollViewSpawner.cs
Assets/Strategic Warfare UI Starter Pack/scripts/ProfileScrollViewSpawner.cs.meta
Assets/Strategic Warfare UI Starter Pack/scripts/ProfileUIPanel.cs
Assets/Strategic Warfare UI Starter Pack/scripts/ProfileUIPanel.cs.meta
Assets/Strategic Warfare UI Starter Pack/scripts/UIDragDropPanel.cs
Assets/Strategic Warfare UI Starter Pack/scripts/UIDragDropPanel.cs.meta
Assets/Strategic Warfare UI Starter Pack/scripts/UILoopFadeColor.cs
Assets/Strategic Warfare UI Starter Pack/scripts/UILoopFadeColor.cs.meta
Assets/Strategic Warfare UI Starter Pack/scripts/UIPanelToggleAutoHide.cs
Assets/Strategic Warfare UI Starter Pack/scripts/UIPanelToggleAutoHide.cs.meta
Assets/UI Toolkit.meta
Assets/UI Toolkit/UnityThemes.meta
Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss
Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss.meta
Docs/Diagnostics/WD16x9_dark_world_DryRun.md
Docs/Diagnostics/WD16x9_dark_world_Promotion.md
_LegacyQuarantine/README.md
_LegacyQuarantine/Scenes/_Recovery.meta
_LegacyQuarantine/Scenes/_Recovery/0 (1).unity
_LegacyQuarantine/Scenes/_Recovery/0 (1).unity.meta
_LegacyQuarantine/Scenes/_Recovery/0.unity
_LegacyQuarantine/Scenes/_Recovery/0.unity.meta
```

Também permanecem fora: .utmp/, *~, Assets/_Recovery/ e Assets/_Recovery.meta, já ignorados. Os arquivos do grupo C não entram no staging.

## GRUPO C — candidatos a voltar ao HEAD antes do staging (NÃO executado)

```text
Assets/TextMesh Pro/Resources/Fonts & Materials/BebasNeue-Regular SDF.asset
Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
ProjectSettings/UnityConnectSettings.asset
Assets/Settings/UniversalRP.asset
Assets/UniversalRenderPipelineGlobalSettings.asset
```

Justificativas acima: dois caches TMP, duas serializações de build URP, ativação UnityConnect sem dependência funcional. Reverter futuramente não significa remover esses arquivos.

## GRUPO D — decisão humana

Nenhuma pendência técnica de inclusão bloqueia o grupo A. Licença/proveniência do Starter Pack continua sem comprovação: permanece EXCLUÍDO, não é autorização de publicação futura. Origem/aprovação das dez artes sem uso só importa se forem incluídas futuramente; neste checkpoint a regra do usuário resolve a exclusão.
A auditoria não certifica direitos autorais de todo o acervo preexistente nem os 32 commits históricos ainda não publicados.

## Estimativa e commits

Grupo A: 602 arquivos, aproximadamente 105.66 MiB mais este manifesto (soma dos arquivos atuais, não tamanho do pack Git/delta). Grupo B: 115 paths; Grupo C: 5 diffs.

Maiores arquivos candidatos:
- `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_Ultrawide_4800x2160.png`: 16.63 MiB.
- `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_4K_3840x2160.png`: 13.42 MiB.
- `Assets/Art/Backgrounds/Home/Home.png`: 10.04 MiB.
- `Assets/Art/Backgrounds/Play/Play.png`: 9.94 MiB.
- `Assets/Art/Backgrounds/Matchmaking/Matchmaking.png`: 9.78 MiB.
- `Assets/WarGame.unity`: 4.85 MiB.
- `Assets/Mapas/Peloponeso/Migration/WD_Map_Peloponeso_TerritoryMask_16x9_v01.png`: 4.23 MiB.
- `Assets/Mapas/Dark World/Migration/WD_Map_DarkWorld_TerritoryMask_16x9_v01.png`: 3.79 MiB.

Git LFS: NÃO necessário neste checkpoint; não configurado. Nenhum candidato aproxima 50/100 MiB.
Recomendo um único checkpoint funcional: scripts, testes, cenas, recursos, metas, fontes e gerador possuem dependências cruzadas. Separar somente a higiene não traz ganho suficiente para fracionar esta consolidação. Publicação e renomeação do repositório são etapas futuras, não autorizadas aqui.

## Verificação final

Somente este manifesto foi escrito nesta tarefa. Índice deve permanecer vazio; nenhum arquivo funcional alterado/deletado. Git permanece main no checkpoint histórico. A checagem estática não executou Unity, Test Runner, build nem staging.
