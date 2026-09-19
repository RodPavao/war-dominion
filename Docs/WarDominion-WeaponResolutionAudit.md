# WAR DOMINION — Auditoria da Resolution das Armas

> **AUDITORIA — NÃO DEFINE NOVAS REGRAS.**
>
> Estado analisado: `72137fcb310e9873d09c1961f553ea717dcc4dbc`, com working tree acumulado da etapa em curso. Código runtime, testes EditMode e `WarDominion-ProductDesign.md` foram comparados. Quando as fontes divergem, este documento registra a divergência sem escolher uma nova regra.

## 1. Executive Summary

- O catálogo runtime contém exatamente 26 IDs oficiais: 8 Apoio, 7 Tática, 6 Estratégica e 5 Especial.
- `GameManager` chama `WDWeaponResolutionPipeline.ResolveRound`; essa é a autoridade efetivamente conectada à partida.
- `WDWeaponExecutionCoordinator` executa as 26 armas oficiais sobre o contexto read-only, mutation port, runtime de jogador, runtime de round e registry de instalações.
- A ordenação de ações congeladas é `Phase → OfficialPlayerOrder → ProgramOrder → ActionId`.
- Foram catalogadas 36 relações com conflito material. Resultado: 29 `DEFINED_AND_IMPLEMENTED`, 1 `DEFINED_NOT_IMPLEMENTED`, 2 `IMPLEMENTED_NOT_DOCUMENTED`, 1 `DOC_CODE_CONFLICT` e 3 `UNDEFINED_PRODUCT_RULE`.
- Foram encontrados 2 bugs/riscos objetivos e 7 regras definidas sem cobertura isolada suficiente.
- A existência do método legado `Resolve` e dos handlers modulares não altera o caminho do `GameManager`, mas mantém uma segunda semântica executável em testes. Isso impede afirmar que existe somente uma implementação comportamental no repositório.

## 2. Architecture

### Caminho conectado à partida

`GameManager` cria um `WDWeaponMatchRuntime`, um `WDWeaponRoundRuntime`, um `WDWeaponInstallationRegistry`, uma `WDGameWeaponMutationPort`, um `WDWeaponExecutionCoordinator` e um `WDWeaponResolutionPipeline`. Em `ResolverFase`, somente `ResolveRound` é chamado.

Fontes autoritativas:

- território: `TerritorioClique`, exposto por `WDUnityGameWeaponMutableStateSource`;
- Energy, cooldown, nível, usos e loadout: `WDWeaponMatchRuntime`;
- instalações: `WDWeaponInstallationRegistry`;
- efeitos temporários do round, históricos e créditos diferidos: `WDWeaponRoundRuntime`;
- escrita: `WDGameWeaponMutationPort`, protegida por `MutationId`;
- intenção/freeze: `WDWeaponPreparationService`, sem aplicar efeitos finais;
- execução: `WDWeaponExecutionCoordinator`;
- orquestração global: `WDWeaponResolutionPipeline.ResolveRound`.

### Caminho modular legado ainda existente

`WDWeaponResolutionPipeline.Resolve`, `RegisterActionHandler`, `RegisterPhaseHandler` e os arquivos `*WeaponHandlers.cs` continuam executáveis. Eles são usados pelas suítes de domínio (`WDSupportWeaponTests`, `WDTacticalWeaponTests`, `WDStrategicWeaponTests` e `WDSpecialWeaponTests`), mas não pelo `GameManager`.

Classificação: **DOC_CODE_CONFLICT**. A documentação declara uma única execução canônica, enquanto o repositório conserva duas implementações comportamentais. O runtime real tem uma única autoridade; o código/testes, não.

## 3. Real Resolution Order

Ordem reconstruída do enum e de `ResolveRound`:

1. `RoundStart (0)`: abre runtime do round, paga Mercenários vencidos, limpa desabilitação temporária, avança Flak, credita Energy de conquistas anteriores e paga Centrais.
2. `Infiltration (5)`: Infiltrado.
3. `Peace (10)`: Paz.
4. `TransferNullifier (20)`: Anulador de Transferência.
5. `TerritoryTransfer (30)`: ações de arma da fase e depois transferências sistêmicas.
6. `Interference (40)`: Sabotagem.
7. `Reinforcement (50)`: reforço natural sistêmico e depois Reforços, Tropas Reservas, Recuperação e Mercenários conforme fase configurada.
8. `InstallationAndLogistics (60)`: instalações e Transporte Aéreo.
9. `Sniper (70)`: Sniper.
10. `Propaganda (80)`: Propaganda de Guerra.
11. `ProgrammedOffense (90)`: bombardeios, Kamikaze, disparos programados de Foguetes/Drone.
12. `AutomaticArtillery (91)`: ações da fase e operação automática sistêmica da Artilharia.
13. `Paratroopers (100)`: Paraquedistas.
14. `TerrestrialActions (110)`: Blitzkrieg quando configurada nessa fase e depois ações terrestres sistêmicas.
15. `Consolidation (120)`: consolidação e encerramento do estado do round.

Dentro de cada fase, `PrepareFrozenActions` ordena por `OfficialPlayerOrder`, depois `ProgramOrder`, depois `ActionId`. Handlers de fase, quando usados pelo caminho legado, ordenam por `Order` e `StableId` antes das ações.

## 4. Canonical Weapon Catalog

| # | ID | Nome | Categoria | Energy | Cooldown | Níveis | Fase |
|---:|---|---|---|---:|---:|---:|---|
| 1 | `air_strike` | Ataque Aéreo | Apoio | 20 | 3 | 6 | ProgrammedOffense |
| 2 | `reinforcements` | Reforços | Apoio | 25 | 3 | 6 | Reinforcement |
| 3 | `air_transport` | Transporte Aéreo | Apoio | 5 | 3 | 6 | InstallationAndLogistics |
| 4 | `flak_tower` | Flak Tower | Apoio | 5 | 3 | 4 | InstallationAndLogistics |
| 5 | `bunker` | Bunker | Apoio | 10 | 3 | 4 | InstallationAndLogistics |
| 6 | `reserve_troops` | Tropas Reservas | Apoio | 15 | 3 | 6 | Reinforcement |
| 7 | `troop_recovery` | Recuperação de Tropas | Apoio | 15 | 3 | 6 | Reinforcement |
| 8 | `mercenaries` | Mercenários | Apoio | 20 | 3 | 6 | Reinforcement |
| 9 | `minefield` | Campo Minado | Tática | 15 | 3 | 6 | InstallationAndLogistics |
| 10 | `blitzkrieg` | Blitzkrieg | Tática | 15 | 3 | 4 | TerrestrialActions |
| 11 | `transfer_nullifier` | Anulador de Transferência | Tática | 5 | 3 | 1 | TransferNullifier |
| 12 | `sniper` | Sniper | Tática | 15 | 3 | 1 | Sniper |
| 13 | `kamikaze` | Kamikaze | Tática | 25 | 3 | 6 | ProgrammedOffense |
| 14 | `paratroopers` | Paraquedistas | Tática | 25 | 3 | 4 | Paratroopers |
| 15 | `radar` | Radar | Tática | 25 | 3 | 1 | InstallationAndLogistics/Preparation imediata |
| 16 | `sabotage` | Sabotagem | Estratégica | 20 | 3 | 6 | Interference |
| 17 | `energy_plant` | Central de Energia | Estratégica | 0 | 5 | 4 | InstallationAndLogistics/RoundStart |
| 18 | `long_range_bomb` | Bomba de Longo Alcance | Estratégica | 25 | 3 | 6 | ProgrammedOffense |
| 19 | `peace` | Paz | Estratégica | 15 | 3 | 1 | Peace |
| 20 | `artillery` | Artilharia | Estratégica | 30 | 3 | 4 | InstallationAndLogistics/AutomaticArtillery |
| 21 | `rockets` | Foguetes | Estratégica | 25 | 3 | 6 | InstallationAndLogistics/ProgrammedOffense |
| 22 | `drone` | Drone | Especial | 30 | 3 | 6 | InstallationAndLogistics/ProgrammedOffense |
| 23 | `atomic_bomb` | Bomba Atômica | Especial | 40 | 3 | 6 | ProgrammedOffense |
| 24 | `war_propaganda` | Propaganda de Guerra | Especial | 25 | 3 | 6 | Propaganda |
| 25 | `chemical_bomb` | Bomba Química | Especial | 25 | 3 | 6 | ProgrammedOffense |
| 26 | `infiltrator` | Infiltrado | Especial | 25 | catálogo 3; execução própria 4 | 1 | Infiltration |

## 5. 26 Weapon Sheets

Legenda de alvos: O=próprio, A=aliado, E=inimigo, N=Neutral, V=Empty. “Falha sem custo” significa que o coordenador não chama `CommitCosts`.

| Arma | Target e momento | Efeito/lifecycle | Bloqueio, consumo, sigilo e interações |
|---|---|---|---|
| Ataque Aéreo | origem O + vizinho E; Resolution 90 | dano imediato; não conquista | Paz impede sem custo; Flak pode interceptar após lançamento; Bunker absorve; custo/cooldown são consumidos quando interceptado. N/V não passam pela validação atual. |
| Reforços | território O/A; Resolution 50 | soma 3–8 tropas | alvo inválido falha sem custo; não conquista; sem munição/persistência. |
| Transporte Aéreo | origem O, destino O/A, quantidade e instalação opcional; Resolution 60 | move tropas e, no máximo, Flak/Central/Artilharia/Foguetes/Drone/Radar; preserva estado e transfere owner no destino aliado | deixa 1 tropa; Mina/Bunker não transportáveis; duplicata do mesmo tipo no destino invalida. Flak não intercepta e Sniper não bloqueia. |
| Flak | instala em O; passiva e duração 3 | instalação transportável; maior nível e ID estável escolhem interceptora | intercepta somente caminhos que chamam `damage.Apply(... allowFlak:true)`: Ataque Aéreo, Longa Distância, Química, Kamikaze, Foguetes, Drone, Atômica central e Paraquedistas. Não intercepta Artilharia, Sniper, Propaganda, Radar ou Transporte. V destrói; N abandona; captura preserva duração. |
| Bunker | instala em O | barreira persistente | absorve dano convencional, inclusive centro atômico e combate de Paraquedistas; Química, Sniper e Propaganda ignoram. V destrói; N abandona; captura preserva HP; não transportável. |
| Tropas Reservas | próprio jogador; Preparation provisória/Resolution 50 | pool por territórios perdidos | Sabotagem não reduz; zero perdas invalida; cancelamento deve reverter benefício e dependências; sem alvo territorial. |
| Recuperação | território O mantido, atacado no round anterior; Preparation provisória/Resolution 50 | recupera até baixas/limite | mudança de owner invalida; cancelamento deve reverter; Sabotagem não reduz. |
| Mercenários | próprio jogador; Resolution 50 + RoundStart | três parcelas, primeira imediata | pagamentos usam IDs idempotentes; Sabotagem não reduz; parcelas persistem independentemente da carta depois da ativação. |
| Campo Minado | território O; Resolution 60 | instalação secreta, uma ativação | persiste em N/V e mantém instalador; dispara na primeira entrada hostil aplicável e é consumida; não transportável/capturável. O caminho canônico atual não contém desarme territorial por Infiltrado. |
| Blitzkrieg | próprio jogador; Preparation provisória/Resolution 110 | aumenta limite terrestre apenas no round | cancelamento restaura limite e remove ordens dependentes; ação invalidada não deve aplicar benefício definitivo. |
| Anulador | jogador E; fase 20 | bloqueia transferência daquele jogador | primeiro válido prevalece; posteriores Superseded sem custo; Paz não é consultada no coordenador. |
| Sniper | território E; fase 70 | mata 1 tropa e, somente após execução válida, marca origem bloqueada | ignora Bunker/Flak/Mina; Paz invalida antes do lançamento; bloqueia ação terrestre e operações de Foguetes, Artilharia e Drone; não interfere com Radar ou Transporte. O pré-registro no freeze foi removido. |
| Kamikaze | origem O com ≥2, alvo E até distância 3; fase 90 | sacrifica 1 no lançamento e causa dano; não conquista | Paz impede antes do sacrifício; Flak pode interceptar, mas sacrifício/custo persistem; Bunker absorve. |
| Paraquedistas | O/A/E/N/V sem distância; fase 100 | reforça, combate ou ocupa V; única arma que conquista | Paz impede E; Flak intercepta; Mina resolve antes; Bunker absorve; custos são comprometidos antes da interceptação; sequência revalida estado vivo. |
| Radar | instalação em O e uso imediato na Preparation | uma revelação privada de cobertura Flak; instalação removida após uso | Flak/Paz/Infiltrado/Sniper não bloqueiam; resultado expira em `BeginRound`; comando idempotente. Radar recém-programado é materializado imediatamente pelo coordenador. |
| Sabotagem | jogador E; fase 40 | reduz somente reforço natural | Paz impede; primeira/melhor seleção segue regra do coordenador; Superseded sem custo; não toca pools de Reservas/Mercenários/Recuperação. |
| Central | instala em O; pagamentos em RoundStart seguinte | três pagamentos, transportável/capturável, preserva pagamentos | não produz no round da instalação; V/N abandona; não opera abandonada; pagamento idempotente. Não há bloqueio por Sniper/Infiltrado/Paz. |
| Bomba Longo Alcance | E/V, qualquer distância; fase 90 | dano imediato; não conquista | Paz bloqueia E; Flak intercepta; Bunker absorve; V executa com zero baixa e custo normal. |
| Paz | jogador E; fase 10, simétrica pelo round | estado temporário | primeira relação válida prevalece; recíproca Superseded sem custo; bloqueia ofensivas que consultam `HasPeace`; não é retroativa. |
| Artilharia | instala O; operação automática fase 91 | munição N1/N2=6, N3/N4=9; até 3 alvos/round | Sniper impede sem munição; próprios/aliados excluídos; Paz exclui inimigos; N/V são alvos; Bunker absorve; Flak não intercepta; captura/transporte preservam tudo. |
| Foguetes | instala O, 2 cargas; disparo programado até distância 3 | pode disparar no round da instalação | Sniper/Paz impedem sem carga; Flak intercepta e ainda consome carga; Bunker absorve; captura/transporte preservam cargas. |
| Drone | instala O, 2 disparos, máx. 1/round/instalação; disparo fase 90 | ofensivo persistente; pode disparar no round da instalação | O/A inválidos; E/N/V válidos; Paz impede E; Sniper não consome disparo; Flak intercepta e consome; Bunker absorve; captura/transporte preservam sequência/cargas. Sem reconhecimento legado no coordenador. |
| Bomba Atômica | E/V central; fase 90 | dano central + 2 em todos os vizinhos congelados; não conquista | Paz protege apenas alvo central E; somente Flak aplicável ao centro intercepta; collateral ignora Paz/Flak e inclui O/A/E/N/V; Bunker é aplicado por território. |
| Propaganda | território E abaixo do limite; fase 80 | transforma em N com `max(1,floor(tropas/2))` | ignora Paz/Bunker/Flak/Mina; instalações não-Mina ficam abandonadas; não conquista; revalida tropas após fases anteriores. |
| Bomba Química | origem O + vizinho E; fase 90 | dano percentual direto; não conquista | Paz impede; Flak intercepta; ignora Bunker; custo após lançamento/interceptação. N/V não passam pela validação atual. |
| Infiltrado | jogador E + WeaponId; fase 5, disponível do round 3 | cooldown forçado 3 na arma alvo; próprio cooldown 4 | pode mirar arma não programada. Se houver ações congeladas da arma, bloqueia as não persistentes. Operações persistentes existentes não são afetadas. Primeiro por ordem canônica para o mesmo `(player, weapon)` prevalece; sem retroatividade. |

## 6. Persistent Installations

| Instalação | Mesmo round | Sniper | Vazio | Neutro | Captura | Transporte | Estado preservado |
|---|---|---|---|---|---|---|---|
| Mina | somente após instalada/entrada posterior | não desarma | permanece, owner original | permanece, owner original | nunca | não | nível/carga |
| Bunker | protege após fase 60 | ignora barreira | destruído | abandonado | sim | não | barreira/nível |
| Flak | protege após fase 60 | não desativa passiva | destruída | abandonada | sim | sim | duração/nível |
| Central | não paga | não bloqueia pagamento | abandonada | abandonada | sim | sim | pagamentos/último round |
| Artilharia | código permite operar no mesmo round (`InstalledRound <= round`) | bloqueia operação | abandonada | abandonada | sim | sim | munição/último round |
| Foguetes | disparo dependente pode ocorrer após instalação | bloqueia disparo | abandonado | abandonado | sim | sim | cargas/nível |
| Drone | disparo dependente pode ocorrer após instalação | bloqueia disparo | abandonado | abandonado | sim | sim | cargas/sequência/último round |
| Radar | scan imediato pode materializar instalação programada | Sniper não interfere | abandonado | abandonado | sim se ainda existir | sim | revelação restante; usado é removido |

Captura e transporte chamam mutações de owner/localização sobre a mesma instância, sem recriar estado, sem Energy, cooldown ou registro de uso.

## 7. Blocking / Defense

### Flak

Intercepta: Ataque Aéreo, Longo Alcance, Bomba Química, Kamikaze, Foguetes, Drone, centro da Atômica e Paraquedistas. Não intercepta: Artilharia, Sniper, Propaganda, Radar, Transporte, collateral atômico ou ações terrestres.

### Paz

`BLOCKED`: ataque terrestre, Ataque Aéreo, Longo Alcance, Química, Kamikaze, Foguetes, Drone, centro atômico, Paraquedistas, Artilharia e Sabotagem.

`NOT BLOCKED`: Propaganda, Anulador, Radar e collateral atômico.

Sniper integra o conjunto bloqueado por Paz; a invalidação ocorre antes de dano,
Energy, cooldown, uso e bloqueio residual.

### Infiltrado

O contrato ativo é `TargetPlayerId + TargetWeaponId`; `TargetActionId` é legado. Ele bloqueia ações congeladas da arma alvo, exceto operações classificadas como instalação persistente, e força cooldown mesmo sem ação programada. A disputa é por `(jogador alvo, arma alvo)`, não por ActionId individual.

## 8. Economic Interactions

- Conquista terrestre de território adversário agenda +5 Energy para a próxima Preparation. Ataque falho, movimento, Vazio e região não geram crédito.
- `ResolutionId`, `CreditId`, `MutationId` e `ActionId` impedem reaplicação em retries conhecidos.
- Central paga somente a partir do round seguinte, três vezes, e não paga abandonada.
- Sabotagem reduz apenas o reforço natural. Reforços, Reservas, Recuperação e Mercenários ficam fora da redução.
- Mercenários agenda três parcelas; primeira na ativação e duas futuras.
- Nenhum efeito implementado cancela, reduz ou antecipa créditos de Energy já agendados.
- **UNDEFINED_PRODUCT_RULE:** não há regra explícita sobre perda/captura da Central depois de um pagamento do mesmo `RoundStart`; a ordem técnica torna o pagamento anterior e não retroativo.

## 9. Ownership / Territory

- Armas ofensivas comuns aplicam dano e lifecycle, nunca owner.
- Paraquedistas é a única arma que chama `MakeOccupied` e pode conquistar.
- Propaganda chama `MakeNeutral`.
- Transporte move tropas e pode transferir owner da instalação selecionada, não do território.
- Dano a zero chama `MakeEmpty`: Bunker/Flak são removidos; Mina permanece; Central/Artilharia/Foguetes/Drone/Radar ficam abandonados.
- Em Neutral, Mina preserva instalador e as demais instalações ficam abandonadas. Em captura posterior, todas exceto Mina recebem o novo owner e preservam estado.

## 10. Relevant Conflict Matrix

| A | B | Relação/ordem | Resultado e consumo | Retroativo? | Status |
|---|---|---|---|---|---|
| Infiltrado | ação da arma alvo | fase 5 antes das demais | bloqueia ação não persistente; alvo não paga; Infiltrado paga e recebe CD4 | não | DEFINED_AND_IMPLEMENTED |
| Infiltrado | arma não programada | fase 5 | força CD3; Infiltrado paga | não | DEFINED_AND_IMPLEMENTED |
| Infiltrado | Infiltrado | ordem oficial/program/action | primeiro força CD/bloqueia posterior | não | DEFINED_AND_IMPLEMENTED |
| múltiplos Infiltrados | mesmo player+weapon | ordem oficial/program/action | primeiro paga; demais Superseded sem custo | não | DEFINED_AND_IMPLEMENTED |
| Infiltrado | operação persistente | fase 5 | não bloqueia operação existente | não | DEFINED_AND_IMPLEMENTED |
| Sniper | ação terrestre | fase 70 antes de 110 | bloqueia origem | não | DEFINED_AND_IMPLEMENTED |
| Sniper | Foguetes/Drone/Artilharia | fase 70 antes de 90/91 | não lança/opera e não consome carga | não | DEFINED_AND_IMPLEMENTED |
| Sniper | Radar imediato | nenhuma interação | Radar opera normalmente | não | DEFINED_AND_IMPLEMENTED |
| Infiltrado | Sniper bloqueado | fase 5 antes de 70 | Sniper não executa e não deixa origem bloqueada | não | DEFINED_AND_IMPLEMENTED |
| Sniper | Transporte Aéreo | nenhuma interação | Transporte opera normalmente | não | DEFINED_AND_IMPLEMENTED |
| Flak | oito ações interceptáveis | seleção maior nível/ID | interceptada consome ação/carga/sacrifício conforme lançamento | não | DEFINED_AND_IMPLEMENTED |
| Flak | Transporte Aéreo | não interceptável | transporte passa sem roll | não | DEFINED_AND_IMPLEMENTED |
| Paz | ofensivas consultadas | fase 10 antes da ofensiva | invalida sem custo/lançamento | não | DEFINED_AND_IMPLEMENTED |
| Paz | Sniper | fase 10 antes de 70 | invalida sem custo, dano ou bloqueio | não | DEFINED_AND_IMPLEMENTED |
| Paz | Propaganda | Propaganda ignora Paz | neutraliza se elegível | não | DEFINED_AND_IMPLEMENTED |
| Paz | collateral atômico | collateral após detonação | dano ocorre | não | DEFINED_AND_IMPLEMENTED |
| Anulador | transferência | 20 antes de 30 | transferência removida antes da mutação | não | DEFINED_AND_IMPLEMENTED |
| múltiplos Anuladores | mesmo jogador | ordem canônica | primeiro paga; posteriores Superseded | não | DEFINED_AND_IMPLEMENTED |
| Sabotagem | reforço natural | 40 antes de 50 | reduz apenas natural | não | DEFINED_AND_IMPLEMENTED |
| Sabotagem | pools especiais | origens separadas | não reduz | não | DEFINED_AND_IMPLEMENTED |
| Reforços | Propaganda | 50 antes de 80 | tropas atuais podem invalidar Propaganda | não | DEFINED_AND_IMPLEMENTED |
| Sniper | Propaganda | 70 antes de 80 | baixa pode tornar alvo elegível | não | DEFINED_AND_IMPLEMENTED |
| Atômica | Flak central | 90 | uma interceptação cancela centro+collateral | não | DEFINED_AND_IMPLEMENTED |
| Atômica | Flak/Paz vizinha | collateral congelado | não protegem vizinhos | não | DEFINED_AND_IMPLEMENTED |
| Química | Bunker | 90 | dano ignora barreira | não | DEFINED_AND_IMPLEMENTED |
| Kamikaze | Flak | sacrifício antes da defesa | sacrifício, Energy e CD permanecem | não | DEFINED_AND_IMPLEMENTED |
| Mina | entrada terrestre em V | porta antes da ocupação | dano; sem sobreviventes mantém V; Mina consumida | não | DEFINED_AND_IMPLEMENTED |
| Mina | Paraquedistas | após Flak | dano antes de reforçar/conquistar; Mina consumida | não | DEFINED_AND_IMPLEMENTED |
| Drone | mesmo round da instalação | fase 60 antes de 90 | instala e dispara; uma vez/round | não | DEFINED_AND_IMPLEMENTED |
| Radar | instalação do mesmo round | uso imediato materializa instalação | paga/instala/consome antes da Resolution | irreversível | IMPLEMENTED_NOT_DOCUMENTED |
| Radar | Sniper programado/executado | nenhuma interação | scan segue regras normais; não existe retry especial | não | DEFINED_AND_IMPLEMENTED |
| Transporte | instalação selecionada | fase 60 | move somente selecionada; preserva estado/owner aliado | não | DEFINED_AND_IMPLEMENTED |
| Captura | instalação abandonada | lifecycle | captura preserva recursos e não cobra | não | DEFINED_AND_IMPLEMENTED |
| Propaganda | instalações | fase 80 | Neutral abandona não-Mina; Mina mantém owner | não | DEFINED_AND_IMPLEMENTED |
| ResolutionId | round repetido | cache do pipeline | nenhum hook/mutação repete | n/a | DEFINED_AND_IMPLEMENTED |
| pipeline legado | pipeline global | duas APIs executáveis | regras podem divergir sem afetar GameManager imediatamente | n/a | DOC_CODE_CONFLICT |

## 11. Documentation × Code × Tests Divergences

1. **Pipeline único:** documentação afirma uma única semântica canônica; código conserva `Resolve` + handlers antigos, e testes ainda os exercitam. Runtime da partida usa apenas `ResolveRound`.
2. **Infiltrado:** trechos históricos dizem `ActionId`, alvo ausente inválido e até desarme territorial; a seção `[REGRA ATIVA]` e o código usam `PlayerId + WeaponId`, permitem arma não programada e não desarmam instalações. Os textos históricos precisam permanecer claramente marcados como superados para evitar leitura conflitante.
3. **Transporte:** trecho histórico afirma que apenas Flak é transportável; seção ativa e código aceitam seis tipos. A seção ativa prevalece como intenção explícita, mas a contradição textual existe.
4. **Artilharia:** trechos históricos falam duração/operações; seção ativa e código usam munição 6/9. Os testes atuais cobrem ambos os modelos em suítes diferentes, reforçando o risco do caminho legado.

## 12. Objective Bugs

### B-01 — Sniper anulado ainda pode bloquear origem — RESOLVIDO

- Severidade: **P1**.
- Arquivo: `WDWeaponExecutionCoordinator.PrepareFrozenActions`.
- Correção: o pré-registro no freeze foi removido. `BlockOrigin` ocorre somente no ramo
  executado e validado do Sniper, depois das validações de adversário e Paz.
- Cobertura: `InfiltratorBlockedSniperProducesNoDamageOrResidualOriginBlock` e
  `PeaceBlocksSniperWithoutDamageCostOrResidualOriginBlock`.

### B-02 — Duas implementações comportamentais podem divergir

- Severidade: **P0 arquitetural** (não há duplicação observada no runtime do `GameManager`, mas há risco de evolução divergente).
- Arquivos: `WDWeaponResolutionPipeline.Resolve` e `*WeaponHandlers.cs` versus `ResolveRound` + `WDWeaponExecutionCoordinator`.
- Impacto: as suítes de domínio podem ficar verdes sobre comportamento que a partida não usa, ou vice-versa.
- Correção sugerida: migrar cobertura ainda válida para o caminho canônico e retirar/neutralizar o executor legado depois de confirmar zero consumidores de produção.

## 13. Product Decisions Resolved in Consolidation

- Paz bloqueia Sniper antes de qualquer efeito ou custo.
- Flak não intercepta Transporte Aéreo.
- Sniper não bloqueia Transporte Aéreo.
- Sniper não interfere com Radar; por isso não existe caso especial de retry do Radar.
- Infiltrado bloqueia usos programados da carta/WeaponId, mas não operações de
  instalações persistentes existentes.

## 14. Remaining Severity / Priority

- P0: 1 — executor modular legado coexistindo com o canônico.
- P1/P2: 0 pendências das decisões consolidadas acima.
- P3: textos históricos devem continuar explicitamente marcados como superados.

## 15. Consolidation Test Coverage

Foram adicionadas coberturas canônicas via `ResolveRound`/coordenador para Sniper
bloqueado por Paz e Infiltrado, Radar independente de Sniper, Transporte independente
de Sniper/Flak, separação entre uso novo e operação persistente sob Infiltrado,
preservação no transporte, pagamentos futuros da Central, privacidade histórica do
Radar e Neutral seguido de captura para as instalações capturáveis definidas.

## 16. LEGACY PIPELINE QUARANTINE PLAN

Classificação atual, sem mover ou apagar arquivos:

- `PURE_LEGACY`: `WDSupportWeaponHandlers.cs`, `WDTacticalWeaponHandlers.cs`,
  `WDStrategicWeaponHandlers.cs`, `WDSpecialWeaponHandlers.cs`. Seus consumidores
  localizados são as suítes antigas de domínio `WDSupportWeaponTests.cs`,
  `WDTacticalWeaponTests.cs`, `WDStrategicWeaponTests.cs` e `WDSpecialWeaponTests.cs`.
- `SHARED_UTILITY`: `WDWeaponSystemDomain.cs`, `WDSupportWeaponDomain.cs`,
  `WDTacticalWeaponDomain.cs`, `WDStrategicWeaponDomain.cs` e
  `WDSpecialWeaponDomain.cs`; tabelas, IDs e contratos ainda servem ao runtime canônico.
- `ACTIVE_CANONICAL`: `WDWeaponResolutionPipeline.cs` (`ResolveRound`),
  `WDWeaponExecutionCoordinator.cs`, `WDRealWeaponEffects.cs`,
  `WDGameWeaponMutationPort.cs`, `WDGameWeaponContext.cs`,
  `WDWeaponMatchRuntime.cs`, `WDWeaponPreparationService.cs` e `GameManager.cs`.
- `UNCERTAIN`: nenhum arquivo inteiro. `WDWeaponResolutionPipeline.cs` é ativo, mas
  contém também a API legada `Resolve`; a futura quarentena exige extrair/remover apenas
  essa superfície depois que a cobertura válida das quatro suítes antigas for migrada.

Plano: migrar cada regra ainda exclusiva das suítes antigas para testes de
`ResolveRound`; comprovar zero consumidor de produção e teste; separar a API `Resolve`
do arquivo ativo; então mover/remover somente os quatro handlers e a superfície antiga
em uma etapa própria, com suíte Unity completa antes e depois.

## 17. Next Validation Sequence

1. Compilar os dois assemblies sem warnings.
2. Executar no Unity os testes canônicos novos e a suíte EditMode completa.
3. Validar Play Mode dirigido para Paz×Sniper, Infiltrado×Sniper e Transporte.
4. Somente após essa validação iniciar a quarentena do caminho legado.
