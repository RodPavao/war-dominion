# APOIO — integração Match UI V2

## Autoridade e decisão visual

- Figma `lHqfLKfZxCsv9xmpCUy5Jl`, página 02 — PARTIDA: composição `2436:80`, componente `2028:31` (root `2028:30`).
- Versão atual do node 2436:80: hub x=290, y=826, 252×145 no espaço lógico 1920×1080. Gap de 5 px à direita do rail; base em y=971. Mesmo referencial top-left da Safe Area em 16:9 e 20:9.
- Fill #0B0E12/94%, contorno #C59B63/1 px, raio 10. Sem título, chevron, botão de fechar ou acentos internos.
- Slots: (11,12) e (132,12), 109×121, gap 12; padding horizontal 11 e vertical 12; raio 6, fill #06090E/72%, contorno #C79447/70% de 1 px. Sem loadout não há texto ou preenchimento fictício.
- APOIO abre; clique interno não fecha; clique externo fecha no release. Captura transparente cobre o Canvas e impede o primeiro toque de atingir mapa/HUD. O frame de fechamento continua bloqueado; um novo toque em frame posterior volta a interagir normalmente. Recolher não cancela a intenção nem a ação programada.
- Modal de tropas de Transporte Aéreo: `155:65`, 760×726 centralizado na Safe Area, geometria interna obtida do Figma; dois vetores oficiais exportados dos nodes `155:437` e `155:440`.
- Não foram usados HUD legado nem páginas de arquivo como autoridade visual.

## Auditoria funcional e classificação

### Distinção de produto: Hub APOIO e Hub de Ação

- **Hub APOIO** (`2436:80`): painel do HUD com os dois slots Support ligados ao loadout real.
- **Hub de Ação**: interface contextual exclusiva de armas com implantação territorial persistente. A instalação permanece no mapa enquanto ativa; em Preparation posterior, tocar nela abre seu hub específico. Nem toda arma Support possui esse hub.
- **Transporte Aéreo**: Support programado na Preparation, não é instalação, não permanece implantado e **não possui Hub de Ação próprio**. A ausência desse hub no Figma é deliberada, não uma pendência. O modal coleta parâmetros durante Preparation; a confirmação programa a ação em AÇÕES PROGRAMADAS e a execução ocorre na Resolution. A ação não deixa avião, ícone ou objeto persistente para interação posterior. Quando permitido, a instalação adicional elegível já implantada na origem é selecionada como parâmetro da ação e transportada junto às tropas na Resolution. Essa instalação permanece distinta do Transporte Aéreo.
- `WDMatchSupportTransportView` é apenas o modal de parâmetros `155:65`, exibido no estágio `Parameters` de AirTransport. Nenhum Hub de Ação do Transporte Aéreo foi criado.
- Os pares `Assets/Resources/UI/Match/Support/ArrowRight.svg/.png` e `ChevronDown.svg/.png` são, respectivamente, a seta de rota (`155:437`) e o chevron do campo de instalação (`155:440`) desse modal de parâmetros. Não representam um Hub de Ação.

| Componente | Classificação / encaminhamento |
|---|---|
| WDMatchWeaponLoadoutSnapshot.SupportSlots | Funcional: dois slots imutáveis capturados do deck, com ID, nível e SlotIndex. |
| WDArmoryCatalogConfig.IsSupportEligible | Funcional: elegibilidade pela categoria; não por nome de classe. |
| WDWeaponMatchUIController.GetSupportCards/SelectWeapon/HandleTerritory/ConfirmTransport | Funcional e conectável; reutilizado. Acrescentadas somente queries de destino, abertura da Preparation e fallback indisponível quando a autoridade não existe. |
| Botão APOIO da V2 | Antes somente visual (callback null); agora abre WDMatchSupportView. |
| WDMatchSupportView / WDMatchSupportTransportView | Apresentação e coleta de intenção; nenhuma execução de efeito/cobrança na View. |
| WDWeaponPreparationService | Autoridade preservada: availability/query/program/cancel/freeze. |
| WDWeaponExecutionCoordinator / WDWeaponResolutionPipeline.ResolveRound | Autoridades preservadas para execução e ordem global. |
| TerritorioClique / WDUnityGameWeaponMutableStateSource | Estado territorial real preservado; nenhum estado paralelo. |
| WarDominionMatchHUD (support/transport legado) | Apresentação legada não reativada; consultada apenas para rastrear contratos. |
| WDMatchSupportUsage | Contrato sem consumidores runtime encontrados; não inventada regra de consumo dos dois slots. |
| Hub Tático / Emojis / Transferência / hubs de instalações | Fora deste bloco. |

Sem MatchSetup/loadout, os dois espaços ficam vazios e desabilitados; não há cartas fictícias. Random Cards não fabrica Support adicional. A disponibilidade vem de GetOption, a marca Programmed das ações reais, e freeze/fechamento de Preparation desabilita novas intenções. Rebind não recria os botões entre pointer down e up.

## Catálogo real e regras

Os oito IDs abaixo são elegíveis por `IsSupportEligible`. Todos têm cooldown configurado de 3 rounds. `UseLimit=0` significa ausência desse limite no catálogo, não ausência do limite de uma programação da carta por Preparation. UseLimit positivo considera usos e ações pendentes. Energia e cooldown são consultados no runtime; os números abaixo documentam o catálogo, não são duplicados como regras na View.

| ID / nome | Energia | UseLimit | Intenção / pré-condição / execução |
|---|---:|---:|---|
| air_strike — Ataque Aéreo | 20 | 1 | Origem própria e alvo inimigo diretamente conectado. Dano por nível, interação com Flak/Bunker, sem conquista. |
| reinforcements — Reforços | 25 | 1 | Território próprio/aliado ocupado. Adição de tropas por nível na execução canônica. |
| air_transport — Transporte Aéreo | 5 | 0 | Origem própria e destino próprio/aliado ocupado, quantidade positiva, capacidade por nível e pelo menos uma tropa restante. Instalação opcional elegível já implantada na origem, escolhida como parâmetro; destino não pode duplicar seu tipo. Domínio preexistente preservado. |
| flak_tower — Flak Tower | 5 | 0 | Território próprio, implantação sem duplicar tipo; níveis 1–4, duração de 3 rounds, interceptação canônica. |
| bunker — Bunker | 10 | 0 | Território próprio, implantação sem duplicar tipo; níveis 1–4, barreira por nível e absorção canônica. |
| reserve_troops — Tropas Reservas | 15 | 0 | Benefício dependente de territórios perdidos no round anterior, tabela por nível/perdas. |
| troop_recovery — Recuperação de Tropas | 15 | 0 | Território próprio atacado no round anterior, baixas >0, mesmo dono no início/fim e atualmente. Limite por nível/baixas. |
| mercenaries — Mercenários | 20 | 0 | Beneficia autor; parcelas por nível no round atual e nos dois seguintes. |

Campo Minado é categoria Tático, embora seu ID esteja declarado em WDSupportWeaponIds: não incluído artificialmente nos slots.

Capacidade de Transporte por nível: 5/7/10/12/15/18, limitada também a tropas da origem menos uma. Nenhum limite é aplicado por contador paralelo da UI.

**Nuance preservada do runtime:** GameManager já aplica benefícios provisórios reversíveis de Tropas Reservas e Recuperação à Preparation e os marca em WDWeaponRoundRuntime. Cancelar reverte os benefícios/distribuições dependentes; o executor consulta WasPreparationBenefitApplied para não duplicá-los. Isso não foi removido nem transformado em efeito executado pela View. A afirmação de ausência de execução antecipada nos novos testes cobre Reforços e Transporte; não redefine essas regras preexistentes.

## Fluxo e input

APOIO → slot real → WDWeaponMatchUIController → targeting/parâmetros existentes → WDWeaponPreparationService → mesma query GetProgrammedActions do drawer oficial AÇÕES → ENVIAR JOGADAS/freeze → pipeline canônico.

O X de AÇÕES permanece a única UI de cancelamento de ações programadas. Nenhuma alteração em WDMatchActionsView. O cancelar do modal de Transporte apenas abandona a intenção ainda não programada.

InputPartida consulta raycast atual também para Support, inclusive o acionador APOIO, antes do cache do InputModule. Toques na superfície são bloqueados no mapa; eventos que ocultam UI mantêm um latch por frame. Com hub aberto, o primeiro toque externo somente fecha; um toque posterior pode prosseguir com o targeting existente. O modal de parâmetros tem backdrop capturador, sem cancelamento externo inventado.

## Fronteiras visuais explícitas

1. Por decisão expressa do Product Owner, **não existe referência aprovada do seletor expandido de TRANSPORTAR INSTALAÇÃO**. A pendência significa exclusivamente a UI para escolher a instalação elegível já implantada na origem que será transportada, nunca criar um Hub de Ação do Transporte Aéreo. O campo fechado NENHUMA é apresentado sem botão/lista inventada; este modal confirma somente tropas. O domínio preexistente permanece intacto. Não são definidas novas opções transportáveis, elegibilidade, custos ou limites.
2. Instalação implantada e seu hub contextual são coisas diferentes. Esta implementação não abre hub automaticamente ao implantar uma carta. Representação territorial e interação posterior com instalação ativa pertencem ao bloco específico.
3. O frame do hub fornece placeholders dos slots, sem variantes detalhadas de custo, cooldown, seleção ou feedback de erro. Os nomes reais substituem os placeholders; disponibilidade/programação/freeze governam interatividade. Não foram inventados badges, indicadores ou novas linhas fora da referência. Apresentação adicional desses dados depende de referência aprovada.
4. O modal de Transporte possui efeitos Figma de blur/sombra não equivalentes ao pipeline uGUI atual. Não foi introduzido blur/pipeline gráfico ou alteração de mapa; essa diferença visual permanece explícita. Validação visual humana ainda necessária.

## Regra atual de Transporte Aéreo

O prompt de atualização visual do Hub APOIO substitui o esclarecimento anterior: o ramo opcional preexistente que consulta e move instalação elegível já implantada na origem está de acordo com a regra atual e deve ser preservado. Nenhuma mudança funcional de Transporte foi feita neste ajuste. O modal continua confirmando tropas; a escolha opcional de instalação permanece pendente de UI própria aprovada.

## Regressões e verificação

- `SupportHubUsesRealSlotsAndCanonicalActionsWithoutEarlyEffects`: 1920/2400×1080, posição relativa ao rail/Safe Area, dois slots reais, custo do catálogo, abertura/close, rebind, alvo inválido/aliado, ausência de efeito/cobrança antecipada, AÇÕES, X/freeze e loadout ausente.
- `SupportFreshRaycastBlocksEntryAndHubWithoutAnInputModuleCache`: mouse/touch, Canvas renderizado em target isolado, primeiro raycast sem InputModule/cache, fora fecha sem atravessar para o controle subjacente, dentro não fecha e um frame posterior libera nova interação. Latch não é pré-acionado para mascarar a assertion de raycast.
- `SupportTransportModalCollectsTroopsWithoutExecutingOrInventingInstallationSelection`: limite real deixando uma tropa, quantidade, comando canônico, ausência de execução antecipada e fronteira do seletor opcional.
- Testes existentes de WDSupportWeaponTests, preparação, execução e Resolution preservados; não duplicados nem executados nesta tarefa.
- Compilação externa das duas assemblies com targets temporários fora do projeto para incluir scripts novos; não requer abrir Unity ou alterar csproj gerado.
- Validação final: Product Owner executa **EditMode → Run All** e verifica os fluxos em 16:9/20:9.
