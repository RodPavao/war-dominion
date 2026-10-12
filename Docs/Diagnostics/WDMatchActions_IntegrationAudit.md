# PARTIDA / AÇÕES — auditoria e integração

## Decisão vigente — 2026-09-26

**AÇÕES / WDMatchActionsView = APRESENTAÇÃO OFICIAL APROVADA.**

O Product Owner aprovou a composição atual, substituindo a decisão anterior de
modernização separada. A View e os seis pares SVG/PNG em uso são oficiais; a separação entre
apresentação e domínio continua preservada.

| Camada | Responsabilidade |
|---|---|
| A — contrato/binding | `IWDMatchWeaponPresentation`, `WDMatchUIPresentationBinding`, `IMatchUICommands`, `MatchUIState` e `WDWeaponMatchUIController` |
| B — lógica funcional | `WDWeaponPreparationService`, GameManager/ports, pipeline e executor canônicos |
| C — apresentação oficial | `WDMatchActionsView`: layout, labels, ícones, scroll e encaminhamento de intenções |
| D — assets oficiais | Target, Shield, ArrowRight, Drone, AlertTriangle e Sparkles; SVG/PNG e metas existentes preservados |

Não há dependência da View no domínio. Programação, queries, cancelamento, freeze,
Sabotagem, Infiltrado, consumo de alvo inválido, envio e Resolution não foram alterados
nesta conferência. Os testes funcionais independentes da apresentação continuam válidos.

## Conferência visual final

Autoridade: Figma `lHqfLKfZxCsv9xmpCUy5Jl`, página `204:632` (02 — PARTIDA),
frame `143:149`, drawer `143:212`, e print aprovado de 2026-09-26 15:07:21.
Popups preservados: Sabotagem `155:262` e Infiltrado `155:327`.

- Drawer x295/y205, 396×854, padding24, cantos direitos12; cantos esquerdos retos.
  Coordenadas locais do espaço de apresentação/Safe Area, sem exceção por mapa.
  Mesma geometria em áreas 1920×1080 e 2400×1080; não desloca HUD/mapa.
- Fundo RGB(11,14,18), alpha95%: divergência deliberada do Figma aprovada pelo Product Owner para legibilidade sem blur.
- Título 300×34 em (24,24), Gabarito SemiBold28; subtítulo em (24,62), altura17,
  SemiBold14. Conteúdo continua vindo do round/quantidade real.
- Botão de recolher removido por decisão final do Product Owner. Lista começa em (24,99),
  viewport348×731; linhas64, passo65, divisórias1, badge32/ícone16, X24.
- Títulos das linhas18; detalhes corrigidos para Gabarito Medium13, altura16.
  Contornos de badges/X corrigidos para1px somente neste drawer.
- Cores semânticas e vetores conferidos contra o contexto atual. Três SVGs diferem
  somente em IDs internos de clipPath; os desenhos permanecem iguais. Nenhum asset
  ou GUID precisou ser substituído.
- Quantidade, nomes, alvos e disponibilidade do X vêm das autoridades reais;
  exemplos fictícios do screenshot não são inseridos no runtime.

### Ajuste final de UX aprovado

O print desta revisão representa 1920×1080. O Product Owner validou separadamente
Edge/20:9. Geometria, lista, fontes, Safe Area e os demais elementos permanecem iguais.
A superfície95% dispensa blur para legibilidade; nenhuma câmera/pipeline foi alterada.
ChevronLeft permanece no projeto, com SVG/PNG/metas e GUIDs intactos, mas não integra
mais a apresentação final de AÇÕES. Nenhum botão visual de fechamento o substitui.

AÇÕES no HUD abre o drawer. Uma camada UI transparente atrás do drawer e à frente
do HUD captura o primeiro clique/toque externo, fechando apenas o drawer no release.
A camada acompanha todo o Canvas, inclusive margens externas à Safe Area. O painel
possui seu próprio receptor de clique: interações internas não fecham. X mantém
exclusivamente o cancelamento canônico da ação, sem fechar o drawer.

InputPartida consulta raycast UI atual do drawer antes de iniciar qualquer seleção,
movimento ou targeting. Isso evita depender do cache do InputModule/ordem de Update
no primeiro toque. O frame do fechamento também é consumido; no frame seguinte,
com a camada removida, um novo toque retorna ao fluxo normal. Não há nova autoridade
de gameplay ou cancelamento.

## Verificação

O teste parametrizado16:9/20:9 verifica geometria,95%, ausência de Collapse e camada
transparente. A integração funcional existente cobre entrada pelo botão AÇÕES,
mouse/touch via PointerEventData, prioridade do raycast externo, fechamento externo,
clique interno, proteção do frame de fechamento e X canônico sem fechar a View.
Unity/Test Runner não foram executados; a execução final fica com o Product Owner.

## Mapa e classificação antes da integração

| Componente | Classe | Situação encontrada / tratamento |
|---|---|---|
| Preparation / programação / cancelamento / freeze | A | Serviço canônico existente; reutilizado |
| Pipeline global / executor / estado territorial | A | Existentes; sem alteração |
| Botão AÇÕES V2 | B/E | Apenas selecionava modo terrestre; conectado ao drawer |
| Drawer / X V2 | C/E | Ausentes na V2; implementados a partir do Figma atual |
| Seleção de arma e alvo territorial | B/E | Controller existente; conectado, clique rejeitado agora permanece consumido |
| Seleção de adversário / arma do Infiltrado | B/E | Estágios funcionais existentes sem apresentação V2; popups conectados |
| Binding de armas | B/E | Controller passado na construção, sem rebind; contrato opcional adicionado |
| FinalMatchHUD / HUDPreparacao / WarDominionMatchHUD | D | Legado já retirado pela composição; não reativado nem redesenhado |
| Apoio / Transferência / Tático / reações / hubs | F | Sem nova implementação; leitura/cancelamento de transferência existente preservados |

## Fluxos e autoridades

Selecionar slot → `WDWeaponMatchUIController.SelectWeapon` →
`GameManager.SelecionarArma` → `WDWeaponPreparationService` → coleta de alvo →
`GameManager.ProgramarArma` → consulta `GetProgrammedActions` → drawer.

Sabotagem seleciona a linha do adversário. Infiltrado seleciona o chip de arma:
o mesmo clique encaminha `SelectTargetPlayer` e `SelectInfiltratorWeapon`, preservando
os estágios do controller e a validação canônica. Aliados não aparecem entre adversários.
Backdrop abandona somente o intent ainda não programado; não cancela ações programadas.

X da arma → `CancelAction` → `GameManager.CancelarAcaoArma` → serviço canônico.
`GetCancellationReason` extrai exatamente as regras que `Cancel` já utilizava:
freeze, preparação fechada, existência/round, autoria e irreversibilidade.
Consulta e comando usam essas mesmas regras. A permissão global de edição permanece
no GameManager. Callback atrasado continua sendo recusado pela autoridade.

X de ordem/reforço usa `IMatchUICommands`; os dois comandos agora publicam o snapshot
após o cancelamento. Transferência existente mantém o comando original.
Nenhum X manipula territórios, efeitos ou uma fila visual autoritativa.

O retorno de `HandleTerritory` significa consumo do clique. Antes, uma programação
rejeitada retornava false e `GameManager.ClicarTerritorio` continuava no fluxo terrestre.
Agora o clique que pertence ao estágio territorial da arma é consumido mesmo se a
validação rejeitar o alvo. A rejeição e o intent continuam no controller/serviço.

ENVIAR JOGADAS, envio de todos, timer e Round não foram modificados. Freeze permanece
em `WDWeaponPreparationService`; resolução em `WDWeaponResolutionPipeline.ResolveRound`,
execução em `WDWeaponExecutionCoordinator` e território em `TerritorioClique` pelo port
`WDUnityGameWeaponMutableStateSource`. Nenhum desses executores foi alterado.
Benefícios provisórios já existentes de Apoio continuam sob GameManager, fora deste escopo.

## Estados apresentados

- Vazio: contador zero, nenhuma ação fabricada.
- Programado: linha derivada da consulta real, identificação/alvo/quantidade quando disponível.
- Disponível/indisponível: cards derivam de `AvailabilityReason`; energia insuficiente fica disabled.
- Cancelável: X consulta a autoridade, respeitando também a permissão global de edição.
- Freeze/envio/resolução: X não permite edição; popup de programação não fica ativo.
- Reabertura/rebind: consulta novamente as autoridades. Chaves locais apenas evitam
  recriação de GameObjects idênticos; não armazenam programação autoritativa.
- Utility ativa: drawer recolhe e popup contextual fica suprimido; não há nova utility authority.

## Limites visuais e próximos blocos

Nos frames consultados, a lista mostra ícones para ataque, reforço, transferência,
Drone, Campo Minado e Sabotagem. Para outras armas, a linha mostra nome/alvo real,
mas não inventa um ícone específico. Referência adicional necessária para ícones dessas
outras armas e variantes decorativas de selecionado/congelado/consumido, se desejadas.

Não foi criado popup genérico de confirmação/erro ou instrução territorial: não havia
referência inequívoca desses estados nos frames consultados. O fluxo territorial existente
continua direto no mapa. Sabotagem/Infiltrado usam os dois popups aprovados localizados.
curadoria visual do fluxo direto do Radar, parâmetros de Transporte Aéreo, instalações/hubs e Resolution complementar
permanecem para seus blocos futuros, sem derivação estética do legado.

## Arquivos e regressões

Runtime: `WDMatchActionsView.cs` (+meta), `WDMatchUIV2.cs`,
`WDMatchUIPresentationBinding.cs`, `WDWeaponMatchUIController.cs`,
`WDWeaponPreparationService.cs`, `MatchUICommands.cs`.
Assets: pasta Actions (+meta), sete SVGs e sete PNGs com seus metas.
Testes: `WDWeaponPreparationServiceTests.cs`, `WDMatchUIV2Tests.cs`.

Oito novos casos: consulta/cancelamento; Sabotagem editável; Sabotagem congelada;
Infiltrado; clique territorial inválido; rebind; geometria1920; geometria2400.
Integração usa serviço/runtime reais com ports de dados da fixture já existente.
Asserts verificam adversário, parâmetro da arma, fila real, ausência de efeito/custo
antecipado, reabertura e recusa após freeze. Não usa fila fake para legitimar a UI.

## Verificação e validação manual

Compilação estática de Assembly-CSharp e Assembly-CSharp-Editor com inclusão temporária
dos fontes novos fora dos csproj gerados. Nenhuma execução de Unity/Test Runner.
Diff/whitespace dos arquivos tocados, metas/referências dos ícones e preservação de
resíduos locais verificados separadamente. Resultados finais no relatório da conversa.

Product Owner: primeiro **EditMode → Run All**. Depois, em16:9 e20:9, programar arma,
abrir/reabrir AÇÕES, cancelar no X, escolher adversário para Sabotagem e arma para
Infiltrado; enviar e verificar bloqueio de edição/resolução habitual. Conferir scroll
e ausência de interação do mapa sob os painéis. Chat/Settings/Regiões permanecem congelados.

## Correção do X — primeiro pointer e tratamento vermelho

Causa: UICompositionRoot.Present reaplica Configure a cada snapshot; BindCommands e
BindWeapons chamavam WDMatchActionsView.Bind, que zerava listKey incondicionalmente.
RefreshList destruía/recriava os botões mesmo com a mesma lista. Um down seguido de
snapshot perdia o objeto destinatário do up/click. A camada externa permanece atrás
do drawer e não foi modificada nesta correção.

Bind agora é idempotente para as mesmas instâncias de comandos/controller. Trocas
reais de autoridade continuam invalidando a apresentação. Não há retry ou delay.
X visual24×24 em (324,20), fonte16 e raio6 preservados; símbolo/borda#D95A5A.
Hit target44×44 em (304,10), dentro da linha64 e viewport348: expansão maior à esquerda
para não ser cortada pelo viewport. Passo65 deixa21px entre targets de linhas vizinhas.
O visual não intercepta raycasts: o Button transparente local recebe o primeiro toque.
Cancelamento continua nas autoridades canônicas; X não fecha drawer nem altera efeitos.
Transferência mantém somente sua integração existente, sem declarar o fluxo finalizado.

Regressão de integração: down -> rebind idêntico/snapshot -> mesmo objeto -> up/click,
mouse/touch, área expandida, vermelho, proteção frozen e preservação da outra ação.
