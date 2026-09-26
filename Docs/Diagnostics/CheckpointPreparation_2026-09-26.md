# Preparação do checkpoint — 2026-09-26

Auditoria estática; nenhum staging, commit, exclusão ou alteração funcional executado.
Branch main, HEAD f672f117b11035a31186c659686f81addb31678f.

## A. Ignore

Adicionados .utmp/, *~, /Assets/_Recovery/ e /Assets/_Recovery.meta.
Não foram ignorados Resources, Diagnostics, fontes, licenças ou geradores.

## B. Recovery — classificação B (histórico/local)

Cada cena foi inspecionada como YAML e seu GUID procurado em Assets, ProjectSettings e Packages. Nenhuma referência de entrada encontrada; nenhuma cena está no EditorBuildSettings. Procurados também caminhos e nomes no código. As referências de saída dessas cópias não as tornam necessárias ao runtime.

| Arquivo | GUID | Decisão |
|---|---|---|
| Assets/_Recovery/0.unity | b2fca5b38edcbbf42a64339d016a4b93 | Ignorar, preservar localmente |
| Assets/_Recovery/0 (1).unity | 90dce25afd79dea469d998faeacee526 | Ignorar, preservar localmente |
| Assets/_Recovery/0 (2).unity | 0302a55539cf71f4789d1d9706016c4a | Ignorar, preservar localmente |
| Assets/_Recovery/0 (3).unity | cb5fe25a437646a42b26ab2fa5d7126e | Ignorar, preservar localmente |

Os quatro .unity.meta acompanham suas cenas; Assets/_Recovery.meta acompanha a pasta. Todos os nove arquivos foram preservados.

## C. LegacyQuarantine

README.md documenta a quarentena de 13/09 e é referência histórica útil. Scenes/_Recovery/0.unity, 0.unity.meta, 0 (1).unity, 0 (1).unity.meta e _Recovery.meta são quarentena local inativa. GUIDs das cenas 3dc6dd1fd6b8c57489a4f3baad916735 e d0ee26beb684eb84aac0586ef7758f16 sem consumidores atuais. Manter as cópias fora do checkpoint; README depende da decisão de preservar esse registro junto do projeto. Nenhum arquivo alterado ou apagado; sem ignore global para esconder documentação.

## D. Starter Pack

Assets/Strategic Warfare UI Starter Pack: 72 arquivos, 53.611.924 bytes (51,13 MiB), além do meta da pasta raiz.
Principais: UI Kit/UI-psd/UI Starter Pack.psd; UI Kit/UI-png; cenas FunctionalUI-Demo e UI-Demo; prefabs de botões, painéis e popups; seis scripts de UI; Strategic Warfare Sci-fi UI Starter Pack Documentation.pdf.
Busca de GUIDs dos metas não encontrou consumidores externos ao pack (desconsiderando recovery); busca das classes e caminhos no código do jogo também não encontrou uso. Nenhum arquivo desse diretório foi identificado como dependência atual do jogo. Dependências internas das demos não constituem dependência do produto.
O PDF foi lido: documentação de uso dos seis scripts, namespace Project.UI e personalização; não contém autorização de redistribuição. Não há LICENSE/README identificado no pack. Excluir integralmente do candidato público até comprovar licença. Não apagado nem ignorado automaticamente.
Limite: referências estáticas não demonstram origem/licença de cópias históricas de arte em outros diretórios.

## E. Todos os novos arquivos maiores que 5 MiB

Valores em MiB. GUID pesquisado em Assets/ProjectSettings; recovery não conta como consumidor runtime. Nenhum LFS configurado.

| Caminho | MiB | Tipo / referência / papel | Recomendação |
|---|---:|---|---|
| `Assets/Art/Backgrounds/Amigos/Amigos.png` | 9.01 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Arsenal/Arsenal.png` | 9.98 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Chat/Chat.png` | 9.97 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Clan/Clan.png` | 10.55 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Configuracoes/Configuracoes.png` | 10.80 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Home/Home.png` | 10.04 | PNG referenciado por Assets/Scenes/Home.unity; arte runtime | Incluir; preferir LFS após decisão, Git normal possível |
| `Assets/Art/Backgrounds/Loja/Loja.png` | 10.56 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Matchmaking/Matchmaking.png` | 9.78 | PNG referenciado por Assets/Scenes/Home.unity; arte runtime | Incluir; preferir LFS após decisão, Git normal possível |
| `Assets/Art/Backgrounds/Missoes/Missoes.png` | 9.76 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Perfil/Perfil.png` | 10.23 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Play/Play.png` | 9.94 | PNG referenciado por Assets/Scenes/Home.unity; arte runtime | Incluir; preferir LFS após decisão, Git normal possível |
| `Assets/Art/Backgrounds/Ranking/Ranking.png` | 10.68 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Backgrounds/Torneios/Torneios.png` | 9.95 | PNG sem referência GUID identificada; arte candidata, necessidade runtime não demonstrada | Decisão pendente; fora até confirmar uso; LFS se incluído |
| `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_4K_3840x2160.png` | 13.42 | PNG referenciado por Assets/Scenes/Home.unity; arte runtime | Incluir; preferir LFS após decisão, Git normal possível |
| `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_4K_3840x2160.png~` | 29.30 | Backup, sem função runtime | Excluir por *~ |
| `Assets/Art/Branding/WarDominion/Loading/WarDominion_Loading_Ultrawide_4800x2160.png` | 16.63 | PNG referenciado por Assets/Scenes/Home.unity; arte runtime | Incluir; preferir LFS após decisão, Git normal possível |
| `Assets/Strategic Warfare UI Starter Pack/UI Kit/UI-psd/UI Starter Pack.psd` | 43.02 | PSD fonte de trabalho; sem consumidor externo identificado | Excluir; licença pendente |

## F. Backups e resíduos

.utmp contém 910 arquivos gerados/local-only. O PNG~ de 29,30 MiB ficou ignorado, não apagado. Nenhum diretório inteiro de diagnósticos foi excluído. Não adicionar recursivamente Assets sem a revisão das pendências abaixo.

## G. UnityConnect

Único diff: m_Enabled 0 -> 1. O diff não comprova ação intencional nem identifica quem o produziu. Compatível com ativação por Editor/serviço, mas origem não demonstrável apenas pelo estado Git. Não há justificativa funcional estabelecida nesta auditoria: deixar fora do candidato, pendente de confirmação; nenhuma reversão.

## H. TMP

BebasNeue-Regular SDF: atlas serializado 1024x1024 -> 1x1; tabelas de 12 glyphs/caracteres -> vazias; retângulos de packing reinicializados. Fonte dinâmica. Classificação B: conteúdo/cache gerado, não mudança de design demonstrada.
LiberationSans SDF - Fallback: 780 linhas adicionadas de ajustes entre pares de glyphs (font features/kerning), não troca de fonte. Classificação B: enriquecimento serializado do cache tipográfico.
Recomendação: ambas pendentes de política de serialização TMP; não reverter nem incluir cegamente. Fontes e licenças originais continuam candidatas necessárias; esta observação é somente sobre esses dois diffs.

## I. Mapas, cenas e settings

- Classic: posições de nomes/contadores, nomes legíveis e escalaContador=1. Alteração funcional do mapa.
- Peloponeso: posições/escala e correções de regiaoId e listas de territórios entre regiões. Alteração funcional, não resíduo.
- Riachuelo: escalaContador=0.51 e posição de contador. Alteração funcional.
- Home.unity: troca do background e referências Play/Matchmaking/abertura 16:9 e ultrawide. Incluir com assets e metas correspondentes.
- WarGame.unity: correções de IDs/traduções territoriais (Alaska/Yukon/Alberta/Ontario/Quebec e Austrálias) e faseAtual 1 -> 0. Incluir como estado funcional atual, mantendo a alteração de fase explícita para revisão.
- MatchFlowConfig: dark_world enabled 1 -> 0, além de whitespace. É mudança funcional de disponibilidade; confirmar intenção antes do checkpoint.
- ProjectSettings: Tonkin Games/War Dominion, landscape, splash e ícone Android, identificador com.wardominion.game, InputActions preloaded; também serialização de ícones/batching/compressão Android. Branding/arte fazem parte do produto atual; revisar conjuntamente as opções de plataforma antes do staging.
- QualitySettings: qualidade corrente 0 -> 3 (High), vSync High 1 -> 0; atualização serializedVersion e meshLodThreshold. Pendente: não assumir mero cache.
- UniversalRP: prefiltering de variantes/keywords mudou; aparenta estado derivado do pipeline/build, não recalibração visual comprovada. Pendente.
- URP Global Settings: m_RuntimeSettings.m_List antes vazio agora com 13 referências. Serialização de pipeline; pendente.

A classificação funcional não substitui validação Unity e não afirma que cada hunk foi manualmente aprovado. Nenhum desses arquivos foi modificado nesta tarefa.

## J. INCLUIR — proposta, não staging executado

- .gitignore e este relatório.
- Alterações de código/testes do jogo em Assets/Scripts e Assets/Editor, com novos .meta.
- Home/Match UI V2, Chat/emoji colorido, Settings, componentes e assets realmente usados em Assets/Resources/UI, com metas.
- Fontes bundled e licenças; catálogo Noto, atlas/sprite asset, SVGs oficiais e licenças; gerador determinístico.
- Home.unity e WarGame.unity com dependências; mapas Classic/Peloponeso/Riachuelo e dados de migração coerentes com o código.
- Docs/Diagnostics: relatórios úteis de implementação/migração e generate_chat_noto_sprite.py. Não excluir por nome de pasta.
- Cinco PNGs runtime identificados na tabela, após decisão sobre Git normal/LFS.

## K. EXCLUIR — proposta

- .utmp/, backups *~, Assets/_Recovery e meta raiz (agora ignorados).
- Cópias de cenas em _LegacyQuarantine/Scenes.
- Starter Pack inteiro e meta raiz enquanto redistribuição não for comprovada, incluindo PSD/demos/scripts/documentação de terceiro.
- Caches/builds já cobertos pelo ignore.
- UnityConnect sem necessidade confirmada e diffs TMP gerados, até decisão explícita; continuam intactos.

## L. DECISÃO PENDENTE

- Licença/proveniência do Starter Pack e eventual arte copiada historicamente.
- Dez backgrounds grandes sem consumidor identificado; manter localmente, não assumir necessidade.
- Git normal ou LFS para os PNGs runtime grandes. Não foi instalado/configurado LFS.
- Política para TMP dinâmico; UnityConnect; Quality/URP/URP Global; opções Android de ProjectSettings; desativação de Dark World; fase inicial serializada da cena.
- README de LegacyQuarantine como registro histórico.
- Build Profiles e UI Toolkit/UnityThemes gerados pelo Editor: revisar necessidade de build antes de inclusão.

## M. Verificação

Nenhum git add foi executado. Nenhum arquivo funcional foi apagado ou editado. Somente .gitignore e este relatório foram escritos nesta tarefa. Verificação final de status, índice vazio, HEAD e whitespace relatada na resposta. O manifesto é orientativo, não uma lista segura para git add automático: dependências e pendências exigem decisão antes do staging.
