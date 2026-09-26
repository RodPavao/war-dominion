# Match UI V2 — Configurações e confirmação de abandono

Referências consultadas, sem alteração no Figma: `139:2` / painel `139:65` e `139:67` / painel `139:130`, arquivo `lHqfLKfZxCsv9xmpCUy5Jl`.

## A–B. Auditoria e classificação

A = autoridade funcional; B = parcialmente existente; C = ausente para esta integração.

| Controle | Classe | Autoridade / decisão |
|---|---|---|
| Volume geral | A | `WDSettingsRepository.SetMasterVolume` → `WDUnitySettingsRuntime.ApplyAudio` → `AudioListener.volume`; persistência `WDPlayerPrefsSettingsStore`, chave `WD.Settings.Local.V1`. |
| Música | B | Campo e setter persistidos existem; runtime não aplica volume de música separadamente. Slider mostra valor real, desabilitado. |
| Efeitos sonoros | B | Campo e setter persistidos existem; runtime não aplica SFX separadamente. Slider mostra valor real, desabilitado. |
| Mute individual | C | Sem autoridade; três ícones oficiais permanecem desabilitados. Não sobrescreve volume com zero para simular mute. |
| Exibir emojis | B | Há filtro privado `hideSocialEmojis` no HUD legado, sem preferência compartilhada ou ligação à V2. OFF/ON desabilitados e neutros. |
| NORMAL/RÁPIDAS | B | `ResolutionSpeed` persiste preferência histórica. HUD legado a carregava; V2 usa `ResolutionSequenceController.PlaybackSpeed` para Fast Forward. Não foi criada ligação ou velocidade adicional; controle neutro/desabilitado. |
| Manual | C | Só apresentação legada, sem rota/URL funcional encontrada. Desabilitado. |
| Voltar à partida | A | `WDMatchUtilityCommands.CloseActive()` → `None`. |
| Solicitar abandono / Cancelar | A | Navegação local da apresentação, com confirmação filha de Settings. |
| Confirmar abandono | A, local | `WDMatchSetupContext.ExitToHome()` já existente. Não constitui implementação de leave multiplayer. |

## C–E. Arquitetura, utility e exclusividade

`WDMatchSettingsView` é construído na mesma Safe Area/Canvas da V2. `UICompositionRoot` injeta o repositório existente e a ação de saída. O adaptador `WDMatchSettingsRuntime` delega áudio ao runtime existente e ignora aplicação de gráficos: construir o repositório não altera resolução, qualidade, FPS ou fullscreen.

`WDMatchUtilityCommands` não foi alterado. O botão existente continua usando `Toggle(Settings)`; a V2 apresenta Settings apenas quando esta é a utility ativa. Abrir qualquer outra utility fecha Settings e vice-versa, sem estado global paralelo. O filtro de backdrop do Chat recebe também o botão Settings pela integração na V2; o arquivo do Chat permanece intacto.

## F–I. Controles disponíveis e lacunas

Sliders uGUI usam o EventSystem existente para mouse/touch, com alvo de 448×44 e glyph visual menor. Volume geral salva/aplica através da autoridade real. Refresh usa `SetValueWithoutNotify`, sem gravações ou callbacks extras ao abrir. Música/SFX exibem preferências reais, mas não fingem efeito sonoro: permanecem desabilitados.

Emojis, combate e Manual permanecem preparados visualmente e desabilitados. Os grupos sem autoridade ativa não mostram a seleção fictícia ON/RÁPIDAS do screenshot. Fast Forward permanece intacto.

## J–N. Retorno, saída, punição e backdrops

- Voltar e backdrop de Settings: `CloseActive()` → `None`, sem recarregar partida.
- Solicitar abandono: oculta/desativa painel Settings, mostra confirmação e mantém utility Settings; não chama saída.
- Cancelar e backdrop da confirmação: voltam ao mesmo painel, preservando valores.
- Cliques internos são consumidos pelos painéis; backdrop não é ancestral dos botões/painéis.
- Durante confirmação, o filtro não permite passagem nem para os toggles de utilities.
- Somente confirmação vermelha chama `WDMatchSetupContext.ExitToHome()`, com guarda contra duplo toque; utility é encerrada antes da saída.
- Autoridade existente limpa `WDMatchSetupContext` e carrega Home em modo Single. Destruição da cena segue o cleanup existente, incluindo `UICompositionRoot.OnDestroy` / `EndMatch` do Chat.
- Não foi encontrada autoridade funcional da punição de 15 minutos para equipe. Permanece lacuna de produto/backend; nenhuma persistência ou punição artificial foi adicionada.

## O–Q. Assets, visual e Safe Area

- `139:192` → `Volume.svg/png` (mesmo glyph utilizado em `139:195` e `139:198`).
- `139:146` → `SliderHandle.svg/png` (reutilizado nos três sliders).
- `LeaveGlow.png`: sombra vermelha procedural transparente do botão `139:206`, não substitui vetor oficial.
- Painel Settings: 720×693,2; confirmação: 400×220.
- Cores: dourado `#C59B63`; superfície `#0B0E12` alpha 0,8; seções alpha 0,7; backdrop preto alpha 0,55.
- Gabarito SemiBold/Medium já incorporadas; títulos 27,2 e 24; subtítulo Settings 11,2, confirmação 16.
- Geometria interna obtida diretamente dos nodes. Os dois painéis ficam centralizados na Safe Area e reduzem escala uniformemente se necessário, com margem mínima de 16 unidades. Não foi criado Canvas adicional nem alterado framing.
- Backdrop usa os cantos do root Canvas, cobrindo também as áreas externas à Safe Area.

## R. Arquivos deste trabalho

Alterados:

- `Assets/Scripts/UI/WDMatchUIV2.cs` — construção, binding e apresentação de Settings; alvo Settings no filtro já usado pelo Chat.
- `Assets/Scripts/UI/UICompositionRoot.cs` — injeção das autoridades de preferências e saída.

Criados:

- `Assets/Scripts/UI/WDMatchSettingsView.cs` e `.meta`.
- `Assets/Scripts/UI/WDMatchSettingsRuntime.cs` e `.meta`.
- `Assets/Editor/Tests/WDMatchSettingsViewTests.cs` e `.meta`.
- `Assets/Resources/UI/Match/Settings.meta`.
- `Assets/Resources/UI/Match/Settings/Volume.svg`, `Volume.png`, `SliderHandle.svg`, `SliderHandle.png`, `LeaveGlow.png`, com seus `.meta`.
- Este relatório.

Arquivos auxiliares de snapshot, geração e logs ficam em `.utmp/settings-*` / `.utmp/prepare-settings-assets.cjs`. Inclusões temporárias nos csproj permitiram compilar arquivos novos sem operar Unity; ambos foram restaurados byte a byte após a verificação.

## S–T. Testes e verificações

`WDMatchSettingsViewTests`: 14 casos cobrindo callbacks após rebinding, toggle, exclusividade nos dois sentidos, retorno/None, cliques internos/backdrops, valores provenientes do repositório, persistência de volume, capacidades indisponíveis, confirmação/cancelamento, saída única, ausência de autoridade de saída, centralização e limites em 1920×1080, 2400×1080 e 900×620.

Compilações sequenciais, `dotnet build --no-restore -v minimal`:

- `Assembly-CSharp.csproj`: 0 erros / 0 avisos.
- `Assembly-CSharp-Editor.csproj`: 0 erros / 0 avisos.

PNG: canal alpha verificado; Volume/SliderHandle têm faixa 0–255; glow 0–51. GUIDs dos assets/scripts novos únicos em Assets. Whitespace dos arquivos novos e diff incremental sem erros. Comparação SHA256 com snapshot anterior: somente os dois arquivos de integração preexistentes acima mudaram em Assets. Home, Chat, Regiões, Menu, mapa, cenas, gameplay, fontes e demais assets preexistentes preservados. Não houve operações Git de escrita nem uso da GUI/Test Runner.

**Não foram executados testes Unity. Compilar os testes não comprova sua aprovação em runtime.**

## U. Divergências técnicas explícitas

- Settings centralizado por exigência de Safe Area; o painel no Figma está em x510/y106, fora do centro geométrico 1920×1080.
- Blur de fundo e sombras difusas dos painéis não foram reproduzidos: a apresentação uGUI atual não oferece backdrop blur, e não foi alterado pipeline/câmera/material global para acrescentá-lo. A transparência, escurecimento e glow vermelho estão presentes.
- Bordas/arredondamento utilizam a fábrica já existente (borda 1,5 e raios inteiros), em vez das frações 0,8/1,2 e alguns raios fracionários do Figma.
- Valores/estados são funcionais ou neutros/desabilitados, não os valores ilustrativos do Figma.
- Fidelidade visual final e mouse/touch precisam de inspeção manual no Unity.

## V. Validação manual mínima

1. Deixar Unity importar os novos scripts/assets e executar `WDMatchSettingsViewTests`; depois `WDMatchUIV2Tests`, `WDMatchChatViewTests`, `WDSettingsTests` e Run All para regressão.
2. Em partida local, abrir Settings; conferir 16:9 e 20:9/Safe Area. Alternar utilities e verificar que só uma fica aberta; testar toggle, Voltar, clique externo e interno.
3. Arrastar volume geral e conferir áudio real/valor ao reabrir. Confirmar controles indisponíveis desabilitados e Fast Forward inalterado.
4. Abandonar partida → confirmar que ainda permanece na batalha. Testar Cancelar, backdrop da confirmação, clique interno e tentativa de interação no mapa; valores devem permanecer.
5. Somente ao testar a saída de fato, pressionar ABANDONAR na confirmação e verificar retorno à Home uma única vez.
