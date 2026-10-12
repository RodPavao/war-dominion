# Transferência, Hub de Emojis e Hub Tático — 2026-09-26

## 1. Autoridades auditadas e reutilizadas

- Transferência: `GameManager` → `SistemaTransferencias.Registrar` → `FilaTransferencias` / `OrdemTransferencia`.
- Resolution permanece no pipeline existente, que chama `ResolvedorTransferencias`. A apresentação não muda dono/tropas nem executa ordens.
- `MatchUIPresenter` publica seleção, destinatários elegíveis e `MatchUIState.Transferencia`; `MatchUICommands` apenas encaminha comandos e solicita atualização.
- Seleção real: `AtualizarHighlightsPreparacao` chama o `TerritorioVisual` existente. Não há card territorial, polígono paralelo ou segunda tecnologia de glow/elevação.
- AÇÕES já apresentava a transferência e chamava `RemoverTransferencia` pelo X. Essa rota foi preservada.
- Comunicação tática já existia em `WDMatchInformationalOverlayController` / `WDTacticalPing`. Foram acrescentados validação de participante existente, evento local e ligação à V2.
- Reações do HUD legado eram apresentação local; nenhuma autoridade de rede existia. `WDMatchReactions` é um contrato de intenção local, separado do repository do Chat.
- Captura: mesmo padrão de AÇÕES/APOIO, com fechamento no click/release, fresh raycast e latch de frame.

## 2. Transferência

O fluxo antigo programava automaticamente quando já havia destinatário. Agora o primeiro toque válido apenas seleciona o território; a confirmação explícita chama a mesma autoridade de registro.

1. Botão TRANSFERÊNCIA alterna o modo canônico; vermelho RGB (0.65, 0.12, 0.12) quando ativo.
2. O modo tem prioridade sobre reforço/movimento/targeting incompatível. Ativá-lo cancela somente seleções transitórias de arma/tática.
3. Território inválido não avança. Território válido mantém o highlight existente.
4. 2×2: único aliado implícito; abre confirmação, sem modal de aliados.
5. 3×3: modal `2292:206`, com os dois destinatários reais; escolher aliado não programa.
6. Confirmar registra a ordem; cancelar/toque externo abandona seleção sem registrar.
7. `↔` de 48 unidades usa `MatchUIState.Transferencia`, resolve o território real e acompanha `MapOverlayProjection.GetTerritoryWorldAnchor` em LateUpdate. Não captura raycast.
8. X de AÇÕES remove a ordem canônica e o indicador desaparece na publicação seguinte.

Não foram alterados custo, limite, elegibilidade, execução ou pipeline de Resolution.

## 3. Apresentação e assets

Autoridades atuais, nunca 99 OLD:

- Transferência ativa: `2292:75`.
- Transferência programada: `2292:141`.
- Escolha de aliado: `2292:206` (560×330).
- Emoji: `122:467`.
- Tático: `131:843`.

Os hubs mantêm os segmentos SVG/PNG existentes, rotações, contornos, cores e efeitos exportados. A View usa coordenadas locais medidas nos frames de 1920×1080, sem rasterizar a composição inteira.

Emoji: seis reações (😎 😢 😘 ✌️ 😁 😂) e setor OCULTAR EMOJIS. Próprio jogador, Preparation e Resolution.

Tático: ATACAR, DEFENDER, FOCO, TRANSFERIR, ATENÇÃO; aliado existente, nunca próprio jogador/adversário, somente Preparation. A opção registra uma intenção passiva para o próximo clique territorial, sem highlights ou modo de seleção; não programa arma ou transferência.

Novos pares SVG/PNG em `Assets/Resources/UI/Match/FigmaCurrent/Hubs/`:

| Asset | Origem |
|---|---|
| EmojiRing | Figma MCP asset `37148c7c-0da9-486b-b2a9-4b1c350c5505.svg` |
| HubArc | Figma MCP asset `e540c6aa-c53b-4448-91dc-c38b7cba5319.svg` |
| HideEye | Figma MCP asset `1b4c793e-b60e-415a-9f75-f524c5a670f1.svg` |
| HideSlash | Figma MCP asset `a38733e5-7946-4567-8552-0be75da0f9f5.svg` |
| TacticAttack | Figma MCP asset `6cc66214-4224-48d2-935a-6bb5a9f02b9c.svg` |
| TacticDefend | Figma MCP asset `a53be61f-346b-49b2-88c2-56bce14b5d01.svg` |
| TacticFocus | Figma MCP asset `543d2235-a271-4019-8180-8bf548b6ed62.svg` |
| TacticTransfer | Figma MCP asset `8df2975a-75ef-448e-9343-8093ca224de4.svg` |
| TacticAttention | Figma MCP asset `be69be05-1f62-4bb3-b57c-ce2de57cfa8f.svg` |
| TacticAttentionDot | Figma MCP asset `e87b9ad0-5e0e-43ac-a54c-77c3fa478a83.svg` |
| NotoKiss | Google Noto Emoji, origem abaixo |

SVGs Figma rasterizados com sharp a 192 dpi. Tamanho visual vem das dimensões do frame, não da resolução da textura.

### Noto / licença

Cinco reações reutilizam `WDChatEmojiColor` com a apresentação de picker. O catálogo/atlas/material/aliases do Chat permanecem 34/34, sem mudanças desta tarefa.

😘 não existe no catálogo de 34. Foi importado somente esse símbolo:

- Origem: https://raw.githubusercontent.com/googlefonts/noto-emoji/e20cbc2bbec1926686be9f9bee7d1d2cfa1fea0e/2D/svg/emoji_u1f618.svg
- Revisão: a mesma de `UI/Chat/Emoji/NotoColor/manifest.json`.
- SHA256 SVG: `e9bd062a90885f5703e151ebd0041879b82881d46ae21fd97bac0364062587a2`.
- Licença Apache-2.0 da pasta `2D/svg`; cópia/NOTICE/AUTHORS já incluídos em `Assets/Resources/UI/Chat/Emoji/NotoColor/`.
- PNG 128×128, sem fonte do sistema; é uma Image de apresentação do hub/reação local. O payload continua Unicode literal 😘.

## 4. Coordenação, input e ciclo de vida

`IWDMatchInteractionPresentation` leva o controller informativo existente até a V2. `WDMatchInteractionView` recebe comandos, snapshot e controller; não acessa mutações do tabuleiro.

Abrir um fluxo incompatível fecha AÇÕES/APOIO/hub e cancela seleção transitória anterior. Nenhum desses fechamentos remove ordens programadas. Há proteção contra republicação síncrona reentrante do binding.

- Overlay aberto: captura externa transparente (dim somente no modal de transferência) cobre o Canvas inteiro, incluindo fora da Safe Area.
- Setores: hit test polar, sem retângulos sobrepostos interceptando o setor vizinho; centro tem alvo próprio.
- Fechamento no release/click, nunca no pointer-down. O mesmo frame fica consumido mesmo após ocultar o overlay.
- Entrada do jogador/Transferência: raycast atual consulta EventSystem independentemente do cache do InputModule.
- InputPartida consulta a proteção antes de collider/contador. A tática somente intercepta alvo válido; alvo inválido descarta a intenção e segue o mesmo gesto normal, inclusive pressão/hold do contador.
- Mudança de fase/identidade, desabilitar View e cancelar modal limpam intenções; programação fica no domínio.
- Sprites e fontes dinâmicas pertencentes à View são liberados; recursos Noto compartilhados não são destruídos.

Safe Area: hubs são posicionados em relação ao avatar acionado (deslocamento lateral canônico), com clamp da composição completa e margem 8. Dimensões de composição: Emoji 461×691; Tático 461×615. Em área menor, escala uniforme limitada pelo espaço disponível. O modal permanece centralizado. Não se altera mapa, câmera, cover, bounds ou LayoutPartida.

## 5. Arquivos desta integração

Criados:

- `Assets/Scripts/UI/WDMatchInteractionView.cs` + meta.
- `Assets/Scripts/UI/WDMatchRadialHit.cs` + meta.
- `Assets/Scripts/UI/WDMatchReactionIntent.cs` + meta.
- `Assets/Editor/Tests/WDMatchInteractionsTests.cs` + meta.
- Os onze pares de assets e seus metas descritos acima.
- Este relatório.

Modificados (preservando mudanças anteriores nos arquivos que já estavam alterados):

- `Assets/Scripts/Core/GameManager.cs`.
- `Assets/Scripts/Core/InputPartida.cs`.
- `Assets/Scripts/UI/MatchUICommands.cs`.
- `Assets/Scripts/UI/WDMatchInformationalOverlayController.cs`.
- `Assets/Scripts/UI/WDMatchUIPresentationBinding.cs`.
- `Assets/Scripts/UI/WDMatchUIV2.cs`.
- `Assets/Editor/Tests/WDWeaponPreparationServiceTests.cs` (apenas novas regressões nesta tarefa).

Os csproj gerados/ignorados foram atualizados localmente para incluir os novos arquivos na compilação estática antes de o Unity regenerá-los.

## 6. Validação

19 casos novos de regressão cobrem fila real, confirmação/duplo comando, aliados, prioridade, cancelamento pelo X existente, marcador derivado, reações, cinco intents táticos, ocultar emojis, hit polar, fase/cleanup, troca de overlays, reentrância e Safe Area 1920/2400×1080.

Os dois casos mouse/touch adicionais em `WDWeaponPreparationServiceTests` reutilizam a fixture já existente de Canvas realmente renderizado e EventSystem isolado, sem InputModule/cache. A fixture verifica entrada, toque interno, down/up/click externo, latch e próximo input, sem acesso a coleção vazia.

Compilação estática final: Assembly-CSharp **0 erros / 0 warnings**; Assembly-CSharp-Editor **0 erros / 0 warnings** (`dotnet build --no-restore`). Unity/Test Runner não executados por esta tarefa. A compilação não substitui a validação visual e comportamental no Player.

Referências dos assets/metas, unicidade dos 22 GUIDs dos assets, catálogo Noto 34/34, existência das fontes e glifos ↔/→ foram verificados estaticamente. Whitespace dos arquivos novos e diff dos arquivos tocados verificados.

Nenhuma operação Git de escrita. Resíduos anteriores preservados. Estado final: branch `main`, HEAD `8b24fc47a57fc913e8ab409a4a30a74957815cc1`, zero staged, 26 arquivos rastreados modificados e 223 untracked (totais incluem trabalho/resíduos anteriores).

## 7. Validação manual obrigatória no Unity

1. EditMode → Run All. Registrar total e eventuais falhas; não assumir resultado a partir da compilação externa.
2. 2×2: TRANSFERÊNCIA fica vermelha; território inimigo não avança; território próprio real destaca; nenhum reforço/movimento é criado. Confirmar sem escolha de aliado; cancelar antes de confirmar não programa.
3. 3×3: selecionar território próprio, escolher exatamente um dos dois aliados e confirmar. Primeiros toques não devem registrar a ordem. Próprio jogador/adversários ausentes das opções.
4. Programada: botão normal, highlight transitório removido, ↔ no território real. Zoom/movimento do mapa acompanha o marcador. X em AÇÕES remove ordem e ↔. Resolution executa pelo fluxo já existente.
5. Toggle OFF e mudança de fase: nenhum território/modal/seleção residual. Abrir APOIO/AÇÕES/Chat/Settings/hub não remove ordens já programadas.
6. Avatar local em Preparation e Resolution: hub radial, seis Noto coloridos e Ocultar. Verificar também 😘; reação local aparece por três segundos. Ocultar suprime a apresentação, não emite sétima reação.
7. Avatar aliado em Preparation: cinco setores táticos nas cores/glows aprovados. Selecionar opção e território produz sinal local, sem efeitos de gameplay. Próprio jogador/adversário não abre hub tático; Resolution não permite tática.
8. Mouse e touch: setor correto, toque interno mantém aberto; primeiro toque externo apenas fecha, sem selecionar território/reforçar/mover/cancelar/acionar botão de baixo. Somente novo toque interage.
9. Repetir em 1920×1080 e 2400×1080/20:9 com Safe Area e avatar próximo aos limites superior/inferior. Conferir setores completos, textos, raio/glow, ícones e relação com o avatar.
10. Reabrir/fechar hubs, trocar fase e sair/voltar à partida; verificar Console sem referências TMP destruídas, seleções presas ou bloqueador residual. Smoke check dos chats e dos módulos congelados.

## 8. Limite de rede

`WDMatchReactions.Requested` e `WDMatchInformationalOverlayController.TacticalPingRequested` são pontos de integração local. Não há envio, recebimento, autenticação, entrega ou sincronização remota simulados. A futura camada multiplayer deve transportar/validar esses intents no contexto da partida.

A fidelidade foi construída a partir dos assets e coordenadas atuais do Figma; a conferência final de renderização Unity, touch físico e aparência 1:1 permanece manual.

## 9. Polimento aprovado após validação 1309/1309

- Coluna: lista de 218 px e painel de 234 px (padding de 8 px), margem direita de 20 px preservada. Altura/avatares/tipografia e espaçamento vertical preservados.
- Radiais: retângulo completo à esquerda da coluna, com 16 px de intervalo e clamp na Safe Area. Geometria interna e assets intactos.
- Reação: centro alinhado ao avatar remetente, 12 px para fora do painel, acompanhando o binding por jogador mesmo após reordenação. Tamanhos 80/63 px, fonte 42 e duração de três segundos inalterados.
- Olho: HideEye permanece; HideSlash aparece apenas quando `Reactions.Hidden` é falso. Texto OCULTAR/MOSTRAR deriva da mesma autoridade.
- Transferência ativa: `TRANSFERÊNCIA ×`, vermelho; toggle funcional preservado. Confirmação: 560×230, somente título e botões; seleção de aliado atualizada para 560×262, título e cards sem textos auxiliares. Destaque usa a seleção territorial existente com luminosidade de 0,55 ± 0,08, restaurada ao sair da confirmação.
- Sinal tático: seleção de território existente permanece como contexto da comunicação. Não executa ação nesse território. Payload inclui remetente/destinatário (IDs e donos), tipo, território, round, fase e partida. `ReceiveTacticalPing` valida contexto/aliados/identidade; a emissão local alimenta essa mesma entrada e o evento de saída, sem transporte de rede.
- Apresentação atual: somente ícone oficial + nome da ação sobre o território. A mensagem junto ao avatar foi removida. Marcador acompanha o território e expira em quatro segundos (`TacticalSignalDuration`), com fade nos últimos 0,5 s. Nada é inserido nas filas de gameplay, AÇÕES ou Resolution. Autenticação/transporte remoto continuam pendentes da camada multiplayer real.
- Regressões: ancoragem dinâmica, separação da coluna em 1920/2400×1080, largura e fonte preservada, olho funcional, rótulo do toggle, modal/highlight e cleanup, payload recebido pelo aliado, rejeição de contexto inválido e expiração. Testes de raycast/captura existentes preservados. Executar Run All e validação visual manual; compilação externa não confirma resultado do Unity.


## 10. Finalização territorial — seção 6 substituída pelo Product Owner

- Eye/Slash: as exportações antigas continham fragmentos invertidos de um olho cortado; não existia olho normal completo. Os dois SVG/PNG existentes foram corrigidos em seus próprios paths/GUIDs: `HideEye` agora é olho completo, `HideSlash` somente diagonal. Hidden mantém olho e oculta diagonal.
- Modal de aliados: removidos objetos Detail/Footer, inclusive textos; título e dois cards reais preservados, cards em y=102 e altura do modal 262. Confirmação continua 560×230.
- A mesma autoridade informativa mantém a intenção pendente e recebe o território. ATACAR/FOCO aceitam inimigo; DEFENDER aceita remetente/time; TRANSFERIR/ATENÇÃO aceitam somente owner destinatário. Neutros são rejeitados. Cliques inválidos descartam a intenção e retornam false; o mesmo gesto segue o pipeline normal, sem payload ou marcador tático.
- Removida a sobreposição de elegibilidade em TerritorioVisual: tática não percorre territórios, não cria highlights e não modifica seleção visual de gameplay. Fase, round, contexto, modo Transferência, abertura de interação incompatível e disable limpam somente a intenção pendente.
- Marcador: ícone oficial, somente label da ação e cor/glow compartilhados com o contrato dos assets do radial, sem nomes ou frases. Reprojeção por frame, deslocamento de 52 px acima da âncora territorial para liberar o contador, clamp no overlay; não captura raycast. Sprites criados para a apresentação são descartados ao expirar; texturas compartilhadas não são destruídas.
- `TacticalPingRequested` entrega o payload completo ao ponto de integração; `ReceiveTacticalPing` valida remetente, destinatário, fase, round, partida e território. Prévia imediata local e entrega local ao destinatário usam a mesma apresentação, sem fingir transporte de rede.
- Setores: corpo SVG .82 → .98; grupo .9 preservado, opacidade efetiva .738 → .882. Cinco SVG/PNG regenerados, mesmas dimensões, paths, bordas e filtros. `WDTacticalVisualStyle` + teste de paridade mantém identificação/ícones/cores/opacidade coerentes entre radial, marcador e fontes SVG.
- Regressões: conteúdo raster real do olho, ausência dos textos, matriz de ownership por ação, emissão única após território válido, ausência de efeitos, ausência de highlights e cleanup, identidade de marcador, projeção em 1920/2400, fade e descarte de sprites. Run All depende de execução do Product Owner; compilação externa não executa testes.

- A cor do marcador é extraída uma vez do RGB predominante do próprio PNG oficial (cache de cor), sem paleta numérica duplicada. Read/Write e importação sem compressão foram habilitados nos cinco ícones táticos pequenos para extrair RGB exato; conteúdo, filtros e GUIDs preservados. O teste confronta a cor extraída com o SVG e o setor correspondente.


## 11. Correção de arquitetura — intenção passiva do próximo clique

- Causa anterior: a intenção havia sido elevada a targeting prioritário no InputPartida, com varredura de candidatos em TerritorioVisual e consumo de clique inválido.
- Modelo atual: `HasPendingTacticalIntent` guarda ação/destinatário/contexto. `TryConsumeTacticalIntent` encerra a intenção em toda tentativa territorial; retorna true exclusivamente quando a comunicação válida foi emitida. Inválido retorna false sem criar payload/marcador.
- Polígono: o retorno false deixa a mesma chamada de GameManager.ClicarTerritorio seguir sua lógica normal. Contador: a observação acontece no próprio encaminhamento normal do contador; inválido continua para IniciarPressaoInput e para o up/hold existente. Nenhuma chamada recursiva, replay ou segundo clique.
- A prioridade tática foi removida do ramo de targeting de arma. Transferência/armas continuam com suas autoridades próprias. A abertura/fechamento de overlays mantém a proteção de eventos UI contra click-through.
- Removidos a varredura de candidatos, lista de referências visuais, parâmetro TacticalEligibility e qualquer destaque prévio tático. Seleção legítima já existente não é modificada pela intenção passiva.
- Marcador, ownership, ícone/nome/cor/glow, ReceiveTacticalPing, duração de quatro segundos, fade, projeção e descarte final permanecem intactos.
- Testes anteriores de consumo inválido/highlight foram substituídos pela matriz válida/inválida/neutra, ausência de highlights, intenção descartada, seleção normal no mesmo clique e pressão/reforço normal do contador. Cleanup cobre cancel, fase, contexto, transferência e disable explícito da View/controller na fixture EditMode.
- Compilação externa: ambas as assemblies sem erros/warnings. Unity/Test Runner não executados. Validação pendente: EditMode → Run All, depois Play Mode.
