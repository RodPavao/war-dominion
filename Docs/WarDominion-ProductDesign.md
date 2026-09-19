# War Dominion — Documento Mestre de Produto e Design

## [IMPLEMENTADO — AGUARDANDO VALIDAÇÃO MANUAL] Etapa 7D.6 — Global Resolution V2

`WDWeaponResolutionPipeline` tornou-se a autoridade lógica única da resolução completa
do round. A Preparation congela somente as intenções; o mapa, os runtimes e o registro
de instalações permanecem vivos e autoritativos. Cada fase revalida o resultado deixado
pelas fases anteriores.

A ordem efetiva é: início do round; Infiltrado; Paz; Anulador; transferências; interferências;
reforços; instalações/logística; Sniper; Propaganda; ofensivas programadas; Artilharia
automática; Paraquedistas; ações terrestres; consolidação. Dentro de cada fase, ações são
ordenadas pela ordem oficial do jogador, `ProgramOrder` e `ActionId`.

O pipeline produz `WDGlobalResolutionResult`, com `ResolutionId`, round, fases, ações,
status técnico e operações sistêmicas. O mesmo `ResolutionId` não executa novamente
custos, pagamentos, armas, transferências ou ações terrestres. O caminho direto anterior
do `GameManager` foi removido: ele apenas fornece callbacks para os resolvedores reais e
consome seus resultados visuais.

As 26 armas possuem identidade canônica; Drone, Radar e Infiltrado usam a execução
canônica concluída no bloco 7D.7A-2. IDs futuros/desconhecidos continuam `Deferred`
tecnicamente. O pipeline não mantém cópia concorrente de tropas,
proprietários, instalações, Energy, cooldowns ou pools.

Limitações preservadas: a distribuição interativa do reforço natural continua no fluxo
de Preparation já existente, enquanto a fase global mantém separadas as fontes de
armas/pools; UI, VFX e backend permanecem fora desta etapa.

## 1. Propósito e autoridade do documento

Este documento é a fonte persistente de verdade para as decisões de produto, direção visual, arquitetura de interface e comportamento de apresentação do War Dominion.

Ele existe para:

- registrar decisões aprovadas sem depender do histórico de conversas;
- distinguir claramente o que já é definitivo do que ainda é provisório, pendente ou futuro;
- orientar implementação, validação e revisão visual;
- impedir que uma prova funcional seja confundida com uma solução visual aprovada;
- evitar regras contraditórias ou obsoletas durante a evolução do projeto.

Regra central:

> **Funcional no Play != visual aprovado.**

Uma funcionalidade operar corretamente no Play Mode valida seu fluxo técnico e sua integração. Isso não aprova automaticamente composição, identidade visual, tipografia, animação, materiais, ícones ou acabamento.

## 2. Sistema de status

Toda decisão relevante deste documento deve ser interpretada por um dos quatro estados abaixo.

### [DEFINITIVO]

Decisão de produto, regra funcional ou direção estrutural aprovada. Só deve ser alterada por nova decisão explícita e registrada.

### [PROVISÓRIO]

Implementação utilizável para validar arquitetura, fluxo ou integração, mas ainda não aprovada como solução final — especialmente no aspecto visual.

### [PENDENTE]

Tema reconhecido que exige definição adicional antes de implementação definitiva. Não deve ser preenchido por suposição.

### [FUTURO]

Evolução planejada fora do escopo imediato. Serve para preservar intenção sem antecipar implementação.

## 3. Metodologia de implementação da interface

### [DEFINITIVO] Passo 1 — implementação global

O primeiro passe estabelece o produto de ponta a ponta:

- telas;
- layout geral;
- navegação;
- fluxos;
- funções;
- integrações;
- estados e comandos necessários.

O objetivo é obter uma experiência funcional, coerente e verificável, sem prolongar prematuramente o polimento de cada elemento.

### [DEFINITIVO] Passo 2 — polimento premium

O segundo passe transforma a fundação funcional na apresentação final:

- arte definitiva;
- molduras e materiais;
- tipografia;
- iconografia;
- glow e iluminação;
- animações e transições;
- partículas;
- áudio de interface;
- responsividade;
- consistência global.

Nenhum elemento do primeiro passe deve ser considerado visualmente definitivo apenas porque está funcional.

## 4. Identidade visual do War Dominion

### [DEFINITIVO] Relação entre Home e HUD de partida

A Home e o HUD de partida pertencem à mesma identidade, mas têm responsabilidades visuais diferentes.

- A Home pode ser mais rica, expressiva e orientada à identidade do jogador.
- O HUD de partida deve ser mais contido, contextual e subordinado ao mapa.
- A Home não deve ser tratada como uma simples variação do HUD.
- O HUD não deve transportar permanentemente toda a densidade informacional da Home.

### [DEFINITIVO] Prioridade do mapa durante a partida

O mapa é o protagonista da tela de partida.

- A interface não deve cobrir áreas importantes do mapa sem necessidade.
- Informações principais devem ocupar bordas e zonas de baixa interferência.
- Informações secundárias devem surgir sob demanda em painéis contextuais, expansões ou overlays.
- Controles devem ser reconhecíveis, compactos e preferencialmente iconográficos.

### [DEFINITIVO] Linguagem de componentes

Evitar aparência padrão do Unity, retângulos simples sem identidade e botões definidos apenas por texto. Componentes devem ser reconhecíveis por forma, iconografia, material, estado e comportamento.

### [DEFINITIVO] Glow como assinatura

O glow é uma assinatura visual central, mas deve ser controlado e significativo.

- Na Preparação, sua intensidade deve ser menor e associada a intenção, seleção e energia contida.
- Na Resolução, pode ganhar intensidade para comunicar execução, impacto e consequência.
- Glow constante e indiscriminado reduz hierarquia e não deve substituir composição ou legibilidade.

### [DEFINITIVO] Materiais e atmosfera permitidos

A linguagem material pode combinar:

- aço e ferro;
- metal desgastado;
- latão e bronze envelhecido;
- ferrugem controlada;
- madeira;
- verde militar e musgo;
- elementos industriais e militares históricos;
- referências vintage de Segunda Guerra Mundial;
- dieselpunk leve.

A combinação desejada é materialidade histórica/militar com iluminação, glow, animação e apresentação modernas.

### [DEFINITIVO] Referências externas

KARDS e WarTime são referências de direção, atmosfera e qualidade. Não autorizam copiar propriedade intelectual, reproduzir composições literalmente ou transformar o War Dominion em derivação visual de outra marca.

## 5. Biblioteca de assets visuais

### [DEFINITIVO] Regra de uso

Os packs externos são matéria-prima, não identidade pronta. Seus elementos podem ser combinados, recortados, recoloridos, convertidos, animados, receber glow, material ou servir de base para componentes próprios.

Nenhum pack externo deve, isoladamente, definir a identidade do War Dominion.

### [DEFINITIVO] MetalBaja

Fonte potencial para estruturas metálicas, chapas, bordas, recortes, desgaste e bases de molduras. Deve ser adaptado à linguagem do projeto e não aplicado como skin genérica em toda a interface.

### [DEFINITIVO] Strategic Warfare UI Starter Pack

Fonte de estudo e matéria-prima para organização militar/estratégica, painéis, controles e elementos de HUD. Não deve ser incorporado de forma literal nem substituir o design system próprio.

### [DEFINITIVO] HYPER

Fonte potencial para efeitos modernos de energia, brilho, transições, feedback e movimento. Seu uso deve ser moderado e integrado à materialidade histórica/militar.

### [DEFINITIVO] Game Icon Pack

Base possível para iconografia funcional. Ícones devem ser selecionados, normalizados e adaptados para manter espessura, escala, contraste e linguagem consistentes.

### [DEFINITIVO] Mechanized Magic

Fonte potencial para efeitos mecânicos, energia, partículas e acentos visuais. Deve apoiar eventos relevantes, não criar excesso de ruído ou deslocar a identidade para fantasia desconectada do produto.

## 6. Home

### [DEFINITIVO] Princípio geral

A Home deve ser mais rica que o HUD de partida e organizar a vida competitiva, social e de progressão do jogador. O botão **PLAY** é o protagonista da composição.

### [DEFINITIVO] Área esquerda

Deve reunir:

- identidade do jogador;
- avatar;
- nickname;
- skin/cor;
- status;
- seletor rápido de conta;
- notificações com badges;
- Missões;
- Ranking;
- Eventos/Torneios.

### [DEFINITIVO] Área central

Deve destacar:

- liga competitiva;
- troféus e progressão;
- PLAY;
- Cards;
- Clan.

### [DEFINITIVO] Área direita

Deve organizar:

- Profile com destaque;
- Friends abaixo de Profile;
- Chat e Settings em escala menor;
- Store e VIP com importância intermediária;
- Discord na área inferior.

### [DEFINITIVO] Perfil e personalização

Profile centraliza a personalização do jogador. Alterações de identidade, apresentação, avatar, skin ou cor devem ser encontradas de forma coerente nessa área, sem dispersar configurações equivalentes por múltiplas telas.

### [DEFINITIVO] Eventos

Eventos e torneios devem ter presença identificável e permitir descoberta de conteúdo competitivo, regras, disponibilidade e progressão associada quando essas definições existirem.

### [DEFINITIVO] Densidade de informação

Informações secundárias devem aparecer em popups, painéis contextuais, janelas ou overlays. A Home não deve permanecer permanentemente congestionada para provar que uma função existe.

### [DEFINITIVO] Composição e escala dos cards

Os cards da Home não devem ocupar ou preencher toda a superfície disponível da tela. A referência estrutural e comportamental é WarTime, sem reprodução literal de sua identidade visual.

A composição final deve favorecer:

- cards menores;
- elementos gráficos;
- espaçamento entre componentes;
- áreas de fundo visíveis;
- sensação de profundidade e composição, em vez de uma grade preenchida;
- tamanhos diferentes conforme a importância;
- informações secundárias sob demanda.

PLAY continua sendo o protagonista, mas não deve terminar como um enorme retângulo simples preenchendo o centro.

A hierarquia de escala desejada é:

- Perfil e Amigos compactos, porém importantes;
- Chat e Configurações pequenos;
- Loja e VIP médios;
- Missões e Ranking compactos;
- Eventos/Torneios com destaque variável e contextual;
- Armas e Clan menores e mais gráficos que na implementação provisória atual.

A Home da Passada 1 valida arquitetura e fluxo. Seu dimensionamento visual atual não é aprovação do tamanho definitivo dos cards.

### [PENDENTE] Definições da Home

Permanecem pendentes:

- fontes, balanceamento e demais usos da economia;
- benefícios de VIP além dos cinco slots de deck;
- modelo da Store;
- detalhes de monetização;
- catálogo de missões;
- limites e faixas das ligas;
- matchmaking.

## 7. Ligas e monetização

### [DEFINITIVO] Estrutura competitiva

- Existem 10 ligas antes da liga Veteran.
- Veteran sucede as dez ligas e admite progressão prática de troféus sem limite fechado definido.
- O sistema deve evitar que veteranos dominem iniciantes por pareamento inadequado.
- O produto não deve ser pay-to-win.

### [FUTURO] [PENDENTE] Modelos econômicos

Podem ser avaliados futuramente, sem decisão detalhada atual:

- VIP/premium;
- cosméticos;
- progressão justa;
- anúncios não intrusivos;
- outras formas de receita compatíveis com competição justa.

Esses modelos não devem conceder vantagem competitiva comprável.

## 8. Modos, matchmaking e jogadores simulados

### [DEFINITIVO] Disponibilidade administrável

Modos e submodos possuem disponibilidade configurável, sem condicionais específicas espalhadas pela Home:

- `Enabled`: aparece e pode avançar no fluxo;
- `ComingSoon`: aparece identificado como indisponível e não inicia fluxo;
- `Disabled`: não aparece para o jogador.

A configuração local por asset/Inspector é a administração da Passada 1. A arquitetura deve aceitar uma fonte remota futura sem exigir que a UI conheça regras de cada modo.

### [DEFINITIVO] Disponibilidade inicial

Inicialmente habilitados:

- FFA;
- 1x1 Normal;
- 1x1 Random Cards;
- 2x2 Normal;
- 2x2 Random Cards.
- 3x3 Normal.

Inicialmente `ComingSoon`:

- 1x1 Deckbuilder;
- Battle Royale.

### [DEFINITIVO] Acesso direto a JOGAR PARTIDA

PLAY abre diretamente JOGAR PARTIDA e a seleção de FFA, 1x1, 2x2 e 3x3. Não existe etapa intermediária com as opções JOGAR, MODOS e MAPAS. Battle Royale não integra a seleção normal e será conteúdo futuro de Eventos/Torneios.

### [PROVISÓRIO] Fluxo JOGAR PARTIDA

Na Passada 1, HOME → PLAY abre diretamente uma seleção horizontal de modos. Os fluxos habilitados respeitam a ordem de formação do grupo, procura de adversários e seleção de mapa, mas usam somente estados locais demonstrativos. Não existe conexão com rede, servidor ou matchmaking real.

### [DEFINITIVO] Regras conceituais de cartas por submodo

- **1x1 Normal:** cada jogador utiliza seu deck padrão.
- **1x1 Random Cards:** cada jogador começa com quatro cartas aleatórias; nos rounds seguintes, ambos recebem a mesma nova carta aleatória, uma por round; não há obrigação de usar as cartas recebidas.
- **2x2 Random Cards:** aplica distribuição aleatória com simetria e equidade entre os lados.

### [FUTURO] 1x1 Deckbuilder

Antes do Round 1, ambos recebem quatro grupos de duas cartas. Em cada grupo, o jogador escolhe uma carta e envia a rejeitada ao adversário. Ao final, ambos formam decks de oito cartas; depois ocorre a entrada na partida e a animação do Round 1.

### [DEFINITIVO] Formação de equipe 2x2

Depois de selecionar Normal ou Random Cards, o jogador escolhe:

- companheiro aleatório;
- companheiro de clã;
- jogar com amigo.

O convite ao clã será oferecido ao clã inteiro. Se o jogador não pertencer a um clã, a interface deve informar essa condição. A primeira aceitação válida preenche a vaga; tentativas posteriores devem receber feedback equivalente a “Vaga já preenchida”.

O fluxo com amigo exige aceite antes de entrar no matchmaking. Duplas formadas por mecanismos diferentes compartilham o mesmo matchmaking.

### [DEFINITIVO] Formação, ordem e transferência 3x3

O 3x3 está disponível somente no submodo Normal nesta fase. Cada partida possui seis participantes individuais: J1/J3/J5 formam Vanguard e J2/J4/J6 formam Sentinel. A ordem inicial é intercalada e a cada round sofre rotação circular para a direita, levando o último participante anterior ao início.

Cada território continua pertencendo a um jogador individual. Movimento de tropas para qualquer aliado preserva o proprietário do destino. Transferência entrega o controle integral do território e preserva suas tropas: com um aliado, como no 2x2, o destinatário é automático; com dois ou mais aliados, como no 3x3, o jogador escolhe explicitamente o destinatário antes da confirmação.

O 3x3 Normal participa da votação de mapas e utiliza deck padrão, sem Random Cards ou Deckbuilder. Battle Royale permanece bloqueado.

### [DEFINITIVO] Ordem pré-partida e privacidade dos adversários

Para todos os modos, quando cada etapa for aplicável, a ordem é: modo/submodo → formação de equipe e confirmação de parceiro(s) → matchmaking com contador crescente → adversário(s) encontrado(s) → votação de mapa → partida. A conclusão da formação de equipe inicia automaticamente o matchmaking, sem comando manual para procurar adversários. O cancelamento é permitido somente durante a procura e fica bloqueado assim que os adversários são encontrados.

- FFA sempre utiliza Classic e não realiza votação de mapa.
- Em 1x1, o jogador escolhe o submodo, procura o adversário, recebe confirmação neutra do encontro e somente então participa da votação.
- Em modos de equipe, a formação e confirmação dos parceiros ocorre antes da procura de adversários; a votação só começa depois que a equipe adversária é encontrada.
- Nickname, avatar, clã, ranking, composição e qualquer outro dado identificável dos oponentes permanecem ocultos durante matchmaking e votação. Essas identidades só podem aparecer com o início efetivo da partida.
- A fila apresenta um contador crescente de tempo de espera em segundos, não uma contagem regressiva.
- O ingresso na fila pode ser cancelado somente enquanto adversários ainda estão sendo procurados. Depois da confirmação do encontro, o cancelamento deixa de estar disponível.
- Encontrar adversários avança automaticamente para a próxima etapa aplicável, sem tela de confirmação ou botão para continuar. O fluxo pré-partida deve minimizar cliques redundantes.

### [FUTURO] Battle Royale

- Battle Royale terá seis jogadores, cinco rounds e modelo Deckbuilder.

### [DEFINITIVO] Elegibilidade e peso dos mapas

Elegibilidade e peso/prioridade de votação são configurados explicitamente por combinação de modo e submodo. A UI não pode inferir elegibilidade pela quantidade de territórios.

Mapas acima do padrão atual de 42 territórios não são inicialmente elegíveis no 1x1 por decisão de configuração, não por regra codificada. Classic pode receber peso maior em modos como 2x2 por configuração igualmente explícita.

### [DEFINITIVO] Regras de votação de mapa

- apresentar dois mapas elegíveis;
- apresentar os candidatos simultaneamente como thumbnails nomeadas e inteiramente clicáveis;
- votação com duração de 15 segundos;
- cada jogador pode votar em um mapa;
- o jogador pode substituir ou remover seu voto enquanto o tempo estiver ativo;
- ausência de voto significa abstenção, sem voto automático;
- vence o mapa com mais votos;
- em empate, sortear apenas entre os mapas empatados;
- seleção de candidatos considera elegibilidade e peso configuráveis.

### [PROVISÓRIO] Implementação local da votação

A Passada 1 possui votação funcional local desacoplada da interface. Ela seleciona até dois candidatos ponderados sem repetição, mostra as artes já cadastradas nas definições dos mapas, aceita no máximo um voto por jogador, permite substituir ou remover a escolha anterior, preserva abstenção e resolve empates aleatoriamente apenas entre os mapas empatados.

A simulação local respeita `PROCURANDO ADVERSÁRIO → ADVERSÁRIO ENCONTRADO → VOTAÇÃO`, quando o modo possui votação. O encontro dispara essa transição imediatamente, sem confirmação intermediária, jogadores fictícios ou dados identificáveis. FFA segue automaticamente do encontro para Classic, sem instanciar votação.

Quando houver somente um mapa elegível com peso positivo, a votação apresenta apenas esse candidato; não inclui mapa inelegível para completar a quantidade e não gera erro. Quando não houver nenhum candidato válido, o fluxo informa a ausência de configuração e não avança silenciosamente.

No teste local, apenas o voto do jogador atual é fornecido. A API aceita votos identificados de múltiplos jogadores para futura integração de rede, mas não inventa participantes online.

### [DEFINITIVO] Match Setup e partida pronta

Ao concluir o pré-jogo, um único contrato independente da UI reúne modo, submodo, mapa e cena selecionados, participantes, equipes, slots, cores, skins, decks, regra de cartas, limite de rounds, morte súbita e seed determinística futura. Esse contrato sobrevive à troca de cena e fica disponível ao bootstrap da partida sem duplicar o estado na interface.

- Cor e skin são preferências do perfil usadas ao entrar na partida.
- Conflitos de cor são resolvidos somente na cor efetiva daquela partida; a preferência salva no perfil nunca é alterada.
- Cada perfil possui cinco slots persistentes de deck. Dois ficam disponíveis sem VIP e os cinco ficam disponíveis com VIP; a expiração do VIP apenas bloqueia os slots 3–5, preservando integralmente nomes e composições.
- Cada deck salvo possui oito Armas principais e duas Armas de Apoio adicionais. A escolha do deck para a partida continua separada das regras de Random Cards e da futura modalidade Deckbuilder.
- Quando o fluxo pré-partida for integrado aos novos decks persistentes, modos com votação que usam deck do perfil poderão selecionar um deck habilitado nos mesmos 15 segundos. A última seleção válida valerá apenas para aquela partida e não alterará o deck padrão.
- FFA não possui votação nem tela de troca de deck: utiliza Classic e o deck padrão automaticamente.
- O limite normal é de 10 rounds. Empate ao final desse limite inicia morte súbita, exceto em modos cuja configuração a desabilita.
- FFA não possui morte súbita.
- Battle Royale possui cinco rounds.
- Random Cards e Deckbuilder são identificados pela regra do submodo no Match Setup, sem antecipar seus sistemas de cartas.

### [PROVISÓRIO] Dados locais de partida pronta

A Passada 1 cria participantes remotos anônimos apenas no contrato interno necessário para validar slots e equipes. Nenhuma identidade adversária é mostrada no pré-jogo. O botão provisório PARTIDA PRONTA carrega a cena registrada nos metadados da definição do mapa; rede, servidor e bootstrap completo do GameManager permanecem fora desta etapa.

### [DEFINITIVO] Contrato unificado de matchmaking

O matchmaking futuro será um serviço unificado e parametrizado, não uma arquitetura independente para cada modo. Seu contrato pode receber:

- modo;
- submodo;
- tamanho do grupo;
- tamanho da partida;
- mapas elegíveis e respectivos pesos;
- regra de cartas;
- permissão de bots;
- formação da equipe.

### [DEFINITIVO] Identidade Guest

Uma identidade automática segue `GuestXXXX`, com exatamente quatro dígitos numéricos. O sistema futuro deverá evitar colisões com nomes Guest atualmente em uso.

O padrão não prova que a entidade seja bot: pode representar bot, jogador que ainda não escolheu nickname ou jogador real que escolheu esse formato.

### [FUTURO] Bots para sustentação do matchmaking

Bots serão necessários para a sustentação inicial do matchmaking e utilizarão identidade `GuestXXXX` dentro das mesmas regras. Ainda assim, o nome isoladamente não prova que uma entidade seja bot. A IA futura não deve ser trivial e poderá considerar territórios, tropas, reforços, ameaças, objetivos, risco, ataques, defesa, cadeias de ataques, cartas, estado do round e comportamento de equipe.

### [FUTURO] Filosofia adaptativa dos bots

Além de jogar estrategicamente, a IA futura deverá observar e se adaptar progressivamente ao comportamento legítimo dos adversários durante a partida. Isso não exige Machine Learning em tempo real: pode ser implementado por memória comportamental, pontuações, perfis dinâmicos, pesos estratégicos e adaptação de decisões.

Entre os sinais observáveis estão:

- agressividade e passividade;
- frequência de ataques;
- preferência por expansão ou fortificação;
- concentração ou dispersão de tropas;
- proteção recorrente de regiões;
- padrões de uso de cartas;
- tipos de movimentos preferidos;
- comportamento sob pressão;
- padrões estratégicos repetidos;
- decisões ofensivas e defensivas que estejam funcionando.

O bot não pode trapacear. Só pode reagir a informações que um jogador legítimo também observaria, sem conhecer cartas ocultas, decisões ainda não reveladas, ações futuras ou estado privado do adversário. A adaptação deve possuir limites configuráveis para não tornar a IA artificialmente perfeita ou impossível de enfrentar.

### [DEFINITIVO] Dois slots adicionais de Armas de Apoio

O conceito anterior de duas cartas externas foi refinado para duas Armas de Apoio adicionais às oito Armas principais. Cada Apoio pode ser usado no máximo uma vez por partida, de forma independente. Não existe troca automática por uma carta principal e a elegibilidade depende de categoria funcional configurada, não de raridade.

A integração dos efeitos concretos dos Apoios com a partida permanece futura. O sorteio de Random Cards e o fluxo da modalidade Deckbuilder continuam sistemas separados.

### [DEFINITIVO] Armamentos, decks salvos e Armas de Apoio

A seção **ARMAS** da Home reúne a coleção e exatamente cinco decks persistentes do jogador. Contas sem VIP utilizam os decks 1 e 2; contas VIP utilizam os cinco. Os decks bloqueados continuam visíveis e não aceitam edição. Expirar o VIP nunca apaga as composições dos decks 3–5, que voltam a ficar disponíveis se o benefício for reativado.

Cada deck possui exatamente oito slots de Armas principais e dois slots adicionais de **Armas de Apoio**. Uma arma não pode se repetir em nenhum dos dez slots do mesmo deck. Os dois Apoios precisam ser diferentes e somente categorias explicitamente configuradas como elegíveis podem ocupar esses slots. O modelo de partida reserva a cada Apoio no máximo um uso independente por partida; usar um não consome o outro. Os efeitos concretos e sua integração ao gameplay permanecem pendentes.

As categorias são classificações funcionais extensíveis — inicialmente Apoio, Tática, Estratégica e Especial — e não representam raridade, força ou qualidade. A arquitetura não depende dessa lista inicial e elegibilidade para Apoio é configuração da categoria.

Cada arma possui de um a seis níveis, conforme sua mecânica. Não criar níveis artificiais apenas para alcançar seis. A progressão é configurável por arma e por nível, sem pressupor dano, duração, chance ou qualquer atributo comum. Upgrades representam Tokens próprios da progressão da arma e custo em Moedas; ambos precisam estar disponíveis e são consumidos conforme a configuração. **Esmeraldas** é o nome definitivo da moeda premium, mas não substitui Tokens ou Moedas e não possui compra real ou vantagem competitiva nesta fase.

### [DEFINITIVO] Progressão V1 e Energy

As armas V1 possuem um, quatro ou seis níveis conforme sua definição. Arma de nível único já é obtida no máximo e não possui upgrade. Energy e cooldown são invariáveis por nível.

| CURVA | UPGRADE | TOKENS ESPECÍFICOS | MOEDAS |
|---|---|---:|---:|
| 6 níveis | N1 → N2 | 100 | 500 |
| 6 níveis | N2 → N3 | 500 | 2.500 |
| 6 níveis | N3 → N4 | 1.000 | 10.000 |
| 6 níveis | N4 → N5 | 5.000 | 25.000 |
| 6 níveis | N5 → N6 | 10.000 | 50.000 |
| 4 níveis | N1 → N2 | 500 | 2.500 |
| 4 níveis | N2 → N3 | 1.000 | 10.000 |
| 4 níveis | N3 → N4 | 10.000 | 50.000 |

Tokens continuam específicos por arma e podem acumular após o nível máximo. Todos os jogadores começam a partida com zero Energy. Valores V1 atualizados: Sniper 15, Central de Energia 0, Transporte Aéreo 5, Flak Tower 5, Bunker 10 e Propaganda de Guerra 25 Energy.

A coleção distingue arma não obtida, progresso parcial de Tokens, Tokens completos com Moedas insuficientes, upgrade disponível e nível máximo. O catálogo data-driven de runtime contém exclusivamente as 26 armas oficiais: 8 de Apoio, 7 Táticas, 6 Estratégicas e 5 Especiais. Mocks de desenvolvimento não pertencem ao catálogo carregado pela Home; cenários isolados de teste devem construir seus próprios fixtures em memória.

O estado local atual utiliza um repositório substituível por conta/backend. Nomes e composições dos cinco decks, inventário, níveis, Tokens, Moedas, Esmeraldas e estado VIP são persistidos sem acoplar a interface ao armazenamento. O visual desta seção é **[PROVISÓRIO]** até o Premium Polish.

A modalidade futura **Deckbuilder** é completamente distinta dos slots de decks salvos: ela construirá dinamicamente um deck antes do Round 1 e não foi implementada nesta fase.

### [DEFINITIVO] Catálogo nominal de 26 armas

1. Ataque Aéreo
2. Campo Minado
3. Sabotagem
4. Reforços
5. Blitzkrieg
6. Drone
7. Bomba Atômica
8. Anulador de Transferência
9. Sniper
10. Central de Energia
11. Transporte Aéreo
12. Kamikaze
13. Bomba de Longo Alcance
14. Paz
15. Artilharia
16. Foguetes
17. Torre Antiaérea / Flak Tower
18. Bunker
19. Propaganda de Guerra
20. Bomba Química
21. Paraquedistas
22. Tropas Reservas
23. Infiltrado
24. Recuperação de Tropas
25. Mercenários
26. Radar

**Anulador** não é uma arma adicional: o nome correto é **Anulador de Transferência**. **Mercenários** é o nome oficial que substitui “Tropas Mercenárias”.

### [DEFINITIVO] Regra global de dano e conquista

Armas que causam dano podem reduzir um território até zero tropas. Quando o dano for maior ou igual à guarnição, o território passa ao estado **VAZIO**, perde o proprietário, permanece com zero tropas e usa a aparência neutra/bege definida para esse estado.

Armas de dano não conquistam território. A conquista ocorre somente por movimento terrestre válido ou por **Paraquedistas**, quando sua mecânica própria de conquista for satisfeita.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Ataque Aéreo

- Energia: 20; uso: 1; cooldown geral previsto: três rounds.
- Alvo: território adversário vizinho.
- Dano por nível: N1 2, N2 3, N3 4, N4 5, N5 6, N6 7.
- Ocorre antes dos movimentos e ataques terrestres do jogador.
- Flak pode interceptar; Paz pode impedir. A ação preparada pode ser bloqueada por Infiltrado através de seu `ActionId`.
- O dano atravessa primeiro a barreira do Bunker; somente o excedente alcança as tropas.
- Não conquista território.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Campo Minado

- Energia: 15; uso: 1.
- É instalado secretamente em território próprio e permanece oculto de adversários e espectadores.
- Permanece instalado por quantos rounds forem necessários até ser acionado; não expira simplesmente ao final do round.
- É acionado pelo primeiro ataque terrestre ou uso aplicável de Paraquedistas naquele território.
- Dano por nível: N1 2, N2 3, N3 4, N4 5, N5 6, N6 7.
- Causa até o número de baixas possível; dano excedente é perdido e, após o acionamento, a mina é consumida.
- Se Paz impedir o ataque antes de ele ocorrer, a mina não é consumida.
- Não é capturável e continua enterrado mesmo se o território ficar Vazio. Infiltrado não desarma Campo Minado nem interfere em instalações persistentes.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Sabotagem

- Energia: 20; uso: 1; alvo: um jogador adversário individual.
- Afeta somente as tropas naturais de reforço daquele round: reforço derivado dos territórios e bônus de regiões.
- Não afeta Reforços, Tropas Reservas, Mercenários nem tropas previamente existentes.
- Percentuais: N1 15%, N2 20%, N3 25%, N4 30%, N5 35%, N6 40%.
- A quantidade sabotada é arredondada para baixo.
- Uma Sabotagem preparada pode ser bloqueada por Infiltrado através de seu `ActionId`.
- Se várias Sabotagens forem preparadas contra o mesmo jogador no mesmo round, prevalece apenas a primeira ação válida conforme a ordem oficial, independentemente do nível. As posteriores são anuladas sem consumo de Energia nem cooldown. Se uma anterior for invalidada por Paz, a próxima válida ainda pode funcionar.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Reforços

- Energia: 25; uso: 1; cooldown geral previsto: três rounds.
- Escolhe um único território próprio ou, em modo de equipe, um território aliado.
- As tropas são adicionadas diretamente ao território escolhido.
- Quantidade por nível: N1 3, N2 4, N3 5, N4 6, N5 7, N6 8.
- Sabotagem não afeta esta arma. A ação preparada de Reforços pode ser bloqueada por Infiltrado através de seu `ActionId`.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Blitzkrieg

- Energia atual: 15; uso: 1.
- O custo permanece provisório até a revisão comparativa global de Energia.
- Concede ataques terrestres adicionais além do limite padrão de três: N1 +1, N2 +2, N3 +3 e N4 +4.
- Os máximos correspondentes no round são 4, 5, 6 e 7 ataques terrestres.
- Possui somente quatro níveis.
- Paz poderá interferir. A ação preparada de Blitzkrieg pode ser bloqueada por Infiltrado através de seu `ActionId`; Infiltrado não bloqueia ações terrestres nem territórios.

### [DEFINITIVO — EXECUÇÃO CANÔNICA] Drone

Drone é uma arma própria do War Dominion.

- Energia: 30; cooldown 3; instalação ofensiva fixa de alcance global.
- A instalação possui exatamente dois disparos totais e pode disparar no mesmo round em que é instalada, respeitando o máximo de um disparo por instalação/round.
- Danos do primeiro/segundo disparo: N1 1/1, N2 2/1, N3 2/2, N4 3/2, N5 3/3 e N6 4/3.
- Instalação e disparos são eventos distintos: Energy/cooldown são comprometidos pela carta; disparos posteriores não geram nova cobrança.
- Captura e Transporte Aéreo preservam carga, sequência e último round de disparo.
- Não conquista território.

### [DEFINITIVO — EXECUÇÃO CANÔNICA] Radar

- Energia: 25; cooldown 3; Tática de nível único.
- Instalação fixa de inteligência, alcance global e exatamente uma revelação.
- Radar recém-instalado na Resolution só pode ser usado a partir da Preparation seguinte.
- O resultado revela somente presença/ausência de cobertura Flak, vale até o fim do round e é privado: somente o autor pode consultá-lo; aliados precisam usar seu próprio Radar.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Central de Energia

- Energia: 0; quatro níveis; cooldown especial de cinco rounds; máximo de duas ativações/instalações por jogador em cada partida.
- Produção por round durante três rounds: N1 +5, N2 +10, N3 +15 e N4 +20 de Energia.
- É uma instalação territorial. Sua força econômica elevada é intencional e não deve ser reduzida automaticamente por parecer uma escolha muito forte.
- Se o território for conquistado, a instalação é capturada e o novo proprietário recebe as parcelas restantes. A duração não reinicia; o conquistador não paga Energia, não consome ativação da própria arma e não altera seu cooldown.
- A instalação em funcionamento pertence ao território e ao proprietário atual, enquanto a ativação original continua atribuída a quem a utilizou.
- Pode ser instalada no Round 1, sem produção retroativa. Uma Central recém-instalada começa a produzir somente no round seguinte; portanto a primeira instalação do Round 1 produz pela primeira vez no início do Round 2.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Bomba Atômica

A Bomba Atômica mantém deliberadamente grande potencial de dano. Seu efeito não deve ser reduzido automaticamente apenas por atingir vários territórios vizinhos: custo elevado de Energia e possibilidade de preparação e defesa estratégica integram o balanceamento. Os detalhes completos ainda serão consolidados na matriz correspondente.

Pode detonar no alvo central programado mesmo que esse território tenha sido zerado anteriormente no mesmo round. A validade é definida pelo alvo central programado. Paz pode impedir o ataque ao próprio alvo central quando aplicável, mas não protege proprietários de territórios vizinhos contra o dano colateral depois de uma detonação válida.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Transporte Aéreo

- Energia: 5; cooldown geral previsto: três rounds.
- Capacidade: N1 5, N2 7, N3 10, N4 12, N5 15 e N6 18 tropas.
- Transporta tropas de território próprio para outro território próprio ou aliado, em qualquer lugar do mapa.
- Deve permanecer ao menos uma tropa na origem quando aplicável às regras normais de propriedade.
- Pode transportar junto uma instalação fixa compatível existente na origem, como Artilharia, Foguetes, Central de Energia ou Flak Tower. A Flak Tower transportada preserva exatamente sua duração restante; o transporte não reinicia essa duração. Campo Minado e Bunker não são transportáveis.
- A instalação mantém integralmente duração, disparos, barreira, produção e demais estados restantes.
- Se o destino já possuir instalação do mesmo tipo, a ação é bloqueada com informação apropriada; tropas e instalação não se movem nem são perdidas. Instalações diferentes podem coexistir no território, salvo futura exceção explícita.
- Flak não intercepta e Paz não bloqueia. A ação preparada de Transporte Aéreo pode ser bloqueada por Infiltrado através de seu `ActionId`.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Kamikaze

- Energia: 25; cooldown geral previsto: três rounds.
- Dano: N1 2, N2 3, N3 4, N4 5, N5 6 e N6 7.
- A origem precisa possuir ao menos duas tropas. Uma delas é usada como Kamikaze e sacrificada.
- Alcance de até três territórios/conexões; não conquista território.
- Flak pode interceptar. Se isso ocorrer, a tropa Kamikaze continua perdida e o alvo não sofre dano.
- Kamikaze reduz primeiro a barreira do Bunker e somente o dano excedente alcança as tropas. Paz poderá impedir a ação; a ação preparada pode ser bloqueada por Infiltrado através de seu `ActionId`.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Bomba de Longo Alcance

- Energia: 25; cooldown geral previsto: três rounds.
- Pode atingir qualquer território inimigo do mapa.
- Dano: N1 2, N2 3, N3 4, N4 5, N5 6 e N6 7.
- Flak pode interceptar; o dano reduz primeiro a barreira do Bunker e somente o excedente alcança as tropas; não conquista território.
- Paz poderá impedir a ação; a ação preparada pode ser bloqueada por Infiltrado através de seu `ActionId`.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Paz

- Energia: 15; nível único.
- Durante a Preparação, jogadores podem programar Paz entre si porque as escolhas ainda não foram resolvidas.
- Na Resolução, é processada conforme a ordem de jogadores daquele round. Uma Paz válida estabelece a relação entre os dois jogadores e impede ações ofensivas posteriores entre eles conforme a matriz final; ambos continuam livres contra outros jogadores.
- Em Paz recíproca, a primeira ação válida estabelece a relação. Quando a segunda chegar à resolução, será apresentada como anulada; seu jogador não perde Energia nem entra em cooldown.
- Regra econômica geral: programar uma arma na Preparação não significa consumir Energia imediatamente. Em regra, o consumo ocorre quando a ação consegue ser executada.
- Paz já efetiva impede Infiltrado quando aplicável. Uma Paz apenas programada no mesmo round ainda pode ser bloqueada antes de ser estabelecida, sem retroatividade.
- Paz não impede Anulador de Transferência, que não é considerado hostilidade territorial abrangida por ela.
- Propaganda de Guerra ignora Paz.
- Transporte Aéreo não interage com Paz por ser ação logística própria ou aliada.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Artilharia

- Energia: 30; cooldown da carta: três rounds; instalação fixa territorial.
- O cooldown controla somente quando o jogador pode usar novamente a carta para criar outra instalação. A duração operacional da instalação já criada é independente e continua sendo consumida normalmente durante o cooldown.
- N1: 1 dano por alvo, até 3 alvos, por 2 rounds.
- N2: 2 danos por alvo, até 3 alvos, por 2 rounds.
- N3: 2 danos por alvo, até 3 alvos, por 3 rounds.
- N4: 3 danos por alvo, até 3 alvos, por 3 rounds.
- É automática: a cada oportunidade operacional, o servidor identifica os territórios diretamente vizinhos que não sejam próprios nem aliados, incluindo territórios inimigos, Neutros e Vazios. Inimigos protegidos por Paz são excluídos; Paz não protege Neutros ou Vazios. Os candidatos são ordenados da maior para a menor quantidade atual de tropas e até três são selecionados. Empates usam ID estável.
- O jogador não escolhe alvos a cada round. A seleção é recalculada em cada operação e pode repetir ou trocar completamente os alvos.
- Pode operar no mesmo round em que é instalada. Resolve depois das demais armas ofensivas programadas e antes de Paraquedistas e ações terrestres, recalculando seus alvos no momento da execução com o estado já atualizado.
- Múltiplas Artilharias resolvem sequencialmente. A ordem entre proprietários segue a ordem oficial dos jogadores; instalações do mesmo proprietário usam o ID estável do território de origem.
- Sniper pode impedir a operação de uma Artilharia já instalada no round. Infiltrado pode bloquear somente a ação que instala uma nova Artilharia, não suas operações persistentes. Só uma operação efetivamente realizada consome a duração operacional; bloqueio por Sniper, ausência de alvos atacáveis ou Paz excluindo todos os alvos inimigos não reduzem as operações restantes.
- Pode ser destruída por armas compatíveis, conforme as regras consolidadas.
- Se o território for conquistado, é capturada e continua para o novo proprietário pelo tempo restante. A duração não reinicia e a captura não consome arma, Energia, ativação ou cooldown do conquistador.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Foguetes

- Energia: 25; cooldown geral previsto: três rounds; instalação fixa territorial.
- Cada instalação possui exatamente dois disparos. Dano por disparo: N1 2, N2 3, N3 4, N4 5, N5 6 e N6 7.
- Não expira por rounds: permanece até consumir os dois disparos, ser destruída ou capturada.
- Alcance de até três territórios/conexões a partir da instalação.
- Não é automática. O jogador programa quando disparar e escolhe um alvo válido; pode instalar e guardar os disparos.
- Pode disparar no mesmo round em que é instalada. A interação com o território da instalação permite escolher usar um disparo e selecionar o alvo válido.
- No máximo uma nova instalação no round permitido pela arma. Podem coexistir instalações do mesmo jogador em territórios diferentes, mas nunca duas de Foguetes no mesmo território. Uma instalação antiga ainda ativa não impede outra quando a arma voltar a estar disponível.
- A contagem é 2 → 1 → 0, quando a instalação é encerrada/removida.
- Se capturada, o novo proprietário recebe exatamente os disparos restantes, sem pagar Energia nem consumir sua própria ativação/cooldown.
- Um disparo lançado e interceptado por Flak consome o tiro. Paz ou Sniper podem impedir o lançamento antes que ocorra, sem consumir o tiro. Infiltrado pode bloquear a instalação programada, mas não um disparo de Foguetes já instalados.
- Resolve antes dos combates terrestres.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Torre Antiaérea / Flak Tower

- Energia: 5; duração: três rounds; instalação secreta com quatro níveis.
- Chance de interceptação: N1 50%, N2 65%, N3 80% e N4 90%.
- Protege o território onde está instalada e territórios próprios ou aliados diretamente vizinhos.
- A matriz final poderá incluir Ataque Aéreo, Drone, Bomba Atômica, Bomba de Longo Alcance, Bomba Química, Kamikaze e Paraquedistas entre as ações interceptáveis.
- Um território não pode conter duas Flaks. Se áreas de Flaks distintas se sobrepuserem, não somar probabilidades nem fazer múltiplas tentativas: usar somente a Flak de maior nível que cobre o alvo.
- Se a Flak que cobre o território central interceptar uma Bomba Atômica, a bomba é destruída antes da detonação e não produz dano central nem colateral.
- Se o território da Flak ficar Vazio, inclusive por Sniper eliminando a última tropa, a instalação é destruída imediatamente. Uma arma posterior no mesmo round não encontra essa Flak; outras Flaks vizinhas ainda podem proteger o local se suas coberturas forem válidas.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Bunker

- Energia: 10; quatro níveis; barreira separada das tropas: N1 2, N2 3, N3 4 e N4 5.
- Funciona como HP de barreira: o dano primeiro reduz a barreira e somente o excedente, após ela chegar a zero, alcança as tropas. Exemplo: barreira 5 contra dano 7 absorve 5, destrói o Bunker e deixa 2 de dano para a guarnição.
- Esta lógica se aplica a Ataque Aéreo, Artilharia, Foguetes, Bomba de Longo Alcance, Bomba Atômica no território central, Kamikaze, forças terrestres e Paraquedistas em ataque.
- Na Bomba Atômica, a barreira protege apenas o território central conforme o dano aplicável; não impede por si só a detonação nem o dano colateral aos vizinhos.
- Não pode haver dois Bunkers empilhados no mesmo território.
- Drone aplica dano primeiro à barreira do Bunker; Flak pode interceptar conforme sua regra própria.
- Sniper ignora a barreira e mata diretamente uma tropa.
- Propaganda de Guerra ignora completamente o Bunker.
- Bomba Química ignora completamente o Bunker, causa baixas diretamente nas tropas e não reduz a barreira.
- Infiltrado não desarma nem modifica Bunker persistente.
- Bunker não pode ser transportado por Transporte Aéreo.
- Se capturado sem ser destruído, passa ao novo proprietário com a barreira restante e sem regeneração automática.
- Se Sniper ou qualquer resultado aplicável eliminar a última tropa e tornar o território Vazio, o Bunker é destruído junto com a perda do domínio.

### [DEFINITIVO] Campo Minado em território Vazio

Se um território chegar a zero tropas, passa a Vazio e o Campo Minado continua enterrado, independentemente do proprietário territorial anterior. A próxima força que tentar entrar sofre a mina, mesmo que pertença a um adversário, aliado do instalador ou ao próprio jogador que a instalou. Depois de o território ficar Vazio, a mina é perigo para qualquer força que entrar.

Campo Minado nunca é capturado e não é desarmado por Infiltrado. Continua não sendo transportável por Transporte Aéreo.

### [DEFINITIVO] Instalações em território Vazio e captura posterior

Quando um território é efetivamente zerado:

- Campo Minado permanece enterrado, nunca é capturado e segue a regra de atingir a próxima força que entrar;
- Bunker é destruído;
- Flak Tower é destruída. Em ação interceptável, ainda tenta interceptar antes do impacto; se falhar e o território for zerado, desaparece. Também desaparece se uma ação não interceptável, como Sniper, zerar o território;
- Artilharia permanece fisicamente instalada e disponível para futura captura;
- Foguetes permanecem instalados, preservam os disparos restantes e ficam disponíveis para futura captura;
- Central de Energia permanece instalada, preserva pagamentos/rounds restantes e fica disponível para futura captura.

Ao ocupar posteriormente um território Vazio com Artilharia, Foguetes ou Central de Energia, o jogador passa a controlar a instalação sem reiniciá-la. Preservar exatamente rounds operacionais, disparos, pagamentos e demais recursos restantes. A captura não restaura recursos consumidos, não cobra Energia nem conta como ativação da arma do novo proprietário. Campo Minado é a exceção e nunca é capturado.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Propaganda de Guerra

- Energia: 25.
- Limite por nível: N1 até 3, N2 até 4, N3 até 5, N4 até 6, N5 até 7 e N6 até 8 tropas. Uma única tropa acima do limite faz a arma falhar completamente.
- Em caso de sucesso, o território perde o proprietário, torna-se Neutro e mantém metade das tropas anteriores, com divisão inteira para baixo: 8 → 4, 7 → 3, 5 → 2 e 3 → 1.
- Ignora Bunker, Campo Minado, Flak e Paz. A ação preparada pode ser bloqueada por Infiltrado através de seu `ActionId`.
- Sniper obrigatoriamente resolve antes de Propaganda. Assim, um território com 9 tropas pode cair para 8 por Sniper e então se tornar elegível para Propaganda N6.
- Reforços e distribuição de tropas ocorrem antes da verificação de seu limite e podem fazer a ação falhar.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Bomba Química

- Energia: 25; alvo: território inimigo diretamente vizinho.
- Dano percentual: N1 25%, N2 30%, N3 35%, N4 40%, N5 45% e N6 50%.
- Arredondar baixas para cima, com mínimo de uma baixa quando a porcentagem positiva aplicada a um alvo válido produzir valor inferior a um. Exemplo: 7 × 50% = 3,5, portanto 4 baixas e 3 sobreviventes.
- Cooldown geral previsto: três rounds. Flak pode interceptar e Paz pode impedir. Ignora completamente o Bunker, causando as baixas diretamente nas tropas sem reduzir a barreira. A ação preparada pode ser bloqueada por Infiltrado através de seu `ActionId`.
- Não conquista território.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Paraquedistas

- Energia: 25; quatro níveis: N1 2, N2 3, N3 4 e N4 5 paraquedistas.
- Pode atuar em qualquer lugar do mapa para atacar território inimigo ou reforçar território próprio/aliado.
- Paraquedistas são tropas reais e constituem a exceção entre as armas: podem conquistar território.
- No ataque, seguem lógica semelhante ao combate terrestre. Precisam atravessar/destruir Bunker; podem acionar Campo Minado; sobreviventes de uma conquista permanecem como tropas do conquistador.
- Se Flak interceptar, nenhum paraquedista chega e a operação é perdida.
- Quando usados como reforço, chegam antes de ataques terrestres programados dependentes daquele território. O combate usa as tropas efetivas após Sabotagem do reforço natural e a chegada dos Paraquedistas.
- Resolve depois dos bombardeios e da Artilharia e antes das ações terrestres.
- Se múltiplas operações chegarem ao mesmo território inicialmente Vazio, a primeira válida pela ordem oficial pode ocupá-lo. Uma chegada aliada posterior reforça o ocupante; uma chegada adversária disputa o território e precisa sobreviver/vencer para conquistá-lo. Campo Minado, Bunker, Flak e demais estados são reavaliados no momento de cada chegada.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Tropas Reservas

- Energia: 15; cooldown geral previsto: três rounds; mecanismo de comeback baseado em territórios perdidos no round anterior.
- Com zero territórios perdidos, não há benefício e a arma não é elegível para produzir efeito.

| NÍVEL | 1 perdido | 2 | 3 | 4 | 5 | 6+ |
|---|---:|---:|---:|---:|---:|---:|
| N1 | 1 | 1 | 2 | 4 | 5 | 6 |
| N2 | 1 | 1 | 2 | 4 | 6 | 7 |
| N3 | 1 | 2 | 2 | 5 | 6 | 7 |
| N4 | 1 | 2 | 2 | 5 | 7 | 9 |
| N5 | 1 | 2 | 3 | 6 | 8 | 10 |
| N6 | 1 | 2 | 3 | 8 | 10 | 12 |

A coluna 6+ representa seis ou mais territórios perdidos. As tropas entram no pool de reforços do jogador. Sabotagem não reduz Tropas Reservas; a ação preparada pode ser bloqueada por Infiltrado através de seu `ActionId`. O servidor deriva as perdas efetivas do round anterior e não confia em informação fornecida pelo cliente.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Infiltrado

- Energia: 20; nível único; cooldown próprio após uso válido: quatro rounds.
- Seleciona um adversário e uma ação de carta programada naquele round pelo `ActionId` exato; não seleciona território nem interfere em instalações persistentes.
- A consulta expõe apenas `ActionId`, `WeaponId` e nome público. Origem, alvo, tropas, parâmetros e instalações permanecem ocultos.
- Resolve em fase própria anterior à Paz e às demais ações anuláveis. Pode bloquear Paz ou instalação de Drone programada naquele round, mas não outro Infiltrado.
- A ação bloqueada não executa nem paga custo/cooldown. Alvo cancelado/ausente invalida o Infiltrado sem cobrança e sem redirecionamento.
- Se vários Infiltrados mirarem o mesmo `ActionId`, o primeiro pela ordem oficial/ordem de programação/ID executa; posteriores ficam `Superseded` sem custo.
- Não bloqueia operações de Drone, Radar, Foguetes ou Artilharia já instalados. Sniper permanece responsável por impedir operações persistentes no round.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Recuperação de Tropas

- Energia: 15; recuperação máxima: N1 4, N2 5, N3 6, N4 7, N5 8 e N6 9.
- Escolhe um território que pertencia ao jogador, foi atacado no round anterior, perdeu tropas e continuou sob domínio do mesmo jogador.
- Recupera diretamente nesse território e não entra no pool geral.
- Nunca ultrapassa as baixas efetivamente registradas ali. Exemplo: perdeu 3 com N6, recupera 3; perdeu 12 com N6, recupera 9.
- O servidor registra as baixas reais por território.

### [DEFINITIVO — IMPLEMENTADO NO DOMÍNIO V1] Mercenários

- Energia: 20; cooldown geral previsto: três rounds.
- Beneficia somente o próprio jogador que ativou a carta; não permite escolher aliado.
- Progressão: N1 1/1/1, N2 2/1/1, N3 2/2/1, N4 2/2/2, N5 3/2/2 e N6 3/3/2.
- A primeira parcela entra no pool de reforços do próprio jogador no round da ativação, antes da distribuição. As duas parcelas seguintes ocorrem automaticamente nos rounds posteriores.
- Sabotagem não reduz Mercenários. Internamente, preservar a origem: reforço natural/regiões é sabotável; Mercenários não é.
- A ação preparada de Mercenários pode ser bloqueada por Infiltrado através de seu `ActionId`; parcelas já estabelecidas não são operações elegíveis.

### [DEFINITIVO] Categoria única de ações terrestres

Ataque terrestre e movimento terrestre não ofensivo pertencem à mesma categoria e ao mesmo limite. Próprio → próprio, próprio → aliado, próprio → inimigo e próprio → Vazio consomem, cada um, uma ação terrestre. Os dois últimos podem produzir respectivamente combate ou ocupação.

O limite normal é de três ações terrestres por jogador/round. Blitzkrieg amplia o total para 4 no N1, 5 no N2, 6 no N3 e 7 no N4. Uma ação programada que se torne inválida devido a alterações anteriores da resolução não acontece, mas seu slot permanece consumido.

### [DEFINITIVO] Transferência territorial na resolução

Transferências entre aliados acontecem no início da resolução. Anulador de Transferência resolve antes delas e não é impedido por Paz. A transferência não exige adjacência, preserva todas as tropas existentes e mantém o limite já definido de uma transferência confirmada por jogador em cada round.

### [DEFINITIVO] Ações programadas contra território que ficou Vazio

Uma ação ofensiva programada não é cancelada apenas porque uma ação anterior zerou o território. Bombardeios podem continuar contra o alvo com zero tropas; podem produzir zero baixas, mas, quando efetivamente executados, mantêm os custos normais. A Bomba Atômica segue sua regra específica de validade pelo alvo central programado.

### [DEFINITIVO] Matriz de interações V2 — concorrência e resolução sequencial

As ações são resolvidas sequencialmente conforme a ordem oficial e critérios determinísticos. Cada ação revalida o estado relevante no momento de sua execução.

- Múltiplas relações independentes de Paz podem coexistir. Em Paz recíproca, a primeira válida estabelece a relação e a segunda é anulada sem Energia/cooldown.
- O primeiro Anulador válido contra um jogador prevalece; os posteriores contra o mesmo jogador são anulados sem Energia/cooldown.
- A primeira Sabotagem válida contra um jogador prevalece independentemente do nível.
- O primeiro Infiltrado válido contra o mesmo `ActionId` prevalece pela ordem oficial;
  os posteriores ficam `Superseded` sem Energy/cooldown.
- Múltiplos Snipers válidos podem atingir sequencialmente o mesmo território, causando uma baixa cada enquanto houver alvo/tropas aplicáveis.
- Múltiplas Propagandas podem ser tentadas em sequência. Falhar pelo limite de tropas não impede uma tentativa posterior de nível suficiente; após um sucesso alterar o estado, ações posteriores incompatíveis deixam de executar.
- Múltiplas armas ofensivas podem atingir sequencialmente o mesmo território. O estado Vazio não cancela bombardeios já programados.

Capturas usam o estado sequencial da resolução. Se um território trocar de proprietário várias vezes no mesmo round, cada conquista válida atualiza o controlador das instalações capturáveis. Ao final, o último proprietário válido mantém essas instalações com seus estados restantes, sem restaurar duração, tiros, pagamentos ou outros recursos consumidos.

### [DEFINITIVO] Categorias funcionais das armas

Categoria organiza identidade e comportamento; não representa raridade nem necessariamente poder.

- **Apoio — 8:** Ataque Aéreo, Reforços, Transporte Aéreo, Flak Tower, Bunker, Tropas Reservas, Recuperação de Tropas e Mercenários.
- **Tática — 7:** Campo Minado, Blitzkrieg, Anulador de Transferência, Sniper, Kamikaze, Paraquedistas e Radar.
- **Estratégica — 6:** Sabotagem, Central de Energia, Bomba de Longo Alcance, Paz, Artilharia e Foguetes.
- **Especial — 5:** Drone, Bomba Atômica, Propaganda de Guerra, Bomba Química e Infiltrado.

Total: 26 armas, distribuídas em 8/7/6/5.

As 26 armas possuem identidade canônica no domínio. A execução canônica de Drone, Radar e Infiltrado está conectada pelo bloco 7D.7A-2 sem pipeline paralelo.

### [DEFINITIVO] Primeira ponte read-only com a partida

A integração das armas começa por um adaptador exclusivamente de consulta entre o estado real da partida e o Weapon Domain. GameManager, Match Setup, GerenciadorRodada, TerritorioClique e a definição canônica do mapa permanecem autoritativos; o domínio de armas não mantém uma segunda cópia concorrente da partida. Escritas, Energy, cooldown, programação e execução de armas continuam fora desta etapa e exigirão portas explícitas próprias.

Identidades técnicas estáveis de jogador, território e mapa permanecem separadas de nomes e textos user-facing. A localização global continua sendo requisito futuro, e nenhum texto traduzido ou apresentado na interface deve ser usado como identidade de domínio.

### [DEFINITIVO] Runtime de loadout, Energy e cooldown

Progressão permanente da conta, loadout disponível na partida e estado runtime são camadas distintas. Energy e cooldown pertencem somente à sessão e nunca são gravados como progressão. Todo jogador inicia com zero Energy; disponibilidade é consultada por códigos técnicos estruturados, que futuramente serão convertidos em mensagens pelo sistema global de localização.

### [DEFINITIVO] HUD funcional de Preparation e Resolution

O HUD usa a mesma tela-base nas duas fases. O deck permanece à esquerda em grade 2 × 4. Em modos de equipe, Transferir e Ações formam um bloco compacto logo abaixo; em 1x1 e FFA, Transferir não reserva espaço e Ações sobe automaticamente para a primeira posição abaixo do deck. O topo esquerdo agrupa Energy, tropas disponíveis e tempo; Menu e Regiões ficam próximos desse painel. O topo direito contém somente Mensagens e Configurações. O botão inferior direito finaliza ações na Preparation e alterna Normal/Acelerado na Resolution, sem pular a resolução.

A disponibilidade visual das cartas representa diretamente o runtime canônico: Energy, cooldown, lock inicial, nível, loadout e limites de uso. Como todos começam com zero Energy, no primeiro round a Central de Energia é a única carta naturalmente utilizável entre cartas com custo positivo; isso não deve ser contornado pela UI. Cartas sem Energy suficiente exibem explicitamente a relação Energy atual/custo. Travas iniciais e cooldowns mantêm também o custo canônico visível, sem confundir o relógio da trava com o cooldown próprio da arma.

Durante cada Preparation, um jogador pode programar no máximo uma nova utilização de cada `WeaponId`. Cancelar validamente essa programação libera a carta para nova programação no mesmo round. Operações de instalações persistentes — como varredura do Radar, disparos de Drone ou Foguetes e operação automática da Artilharia — não constituem segunda utilização da carta.

Regiões é uma camada estritamente informativa baseada em `DefinicaoMapa.Regioes`: colore temporariamente os territórios, mostra nome e bônus reais e bloqueia a interpretação de cliques como gameplay enquanto estiver ativa. Desativar restaura a aparência territorial canônica.

Durante um targeting territorial explícito de arma, toda a área do território — inclusive o contador de tropas — é interpretada primeiro como alvo da arma. Fechar um modal de seleção ainda incompleta cancela o fluxo compartilhado, limpa o targeting e não cria ação nem consome Energy, cooldown ou limite de programação. Esses comandos funcionais permanecem desacoplados da composição visual provisória para futura ligação à UI importada do Figma.

Durante a Preparation, tocar no perfil de aliado oferece Atacar, Defender, Transferir, Atenção e Foco. Esses marcadores são comunicação privada da equipe: não entram em filas, não gastam recursos, não alteram tropas/proprietário e são limpos ao sair da Preparation. Durante a Resolution, tocar no próprio perfil abre emojis e o filtro local de recepção. O resultado social não interfere em sinais táticos.

Uma conquista confirmada pelo evento canônico de resolução solicita brilho forte sobre a forma inteira do território durante 1,5 segundo. A apresentação não calcula nem modifica a conquista.

A seleção terrestre usa ressalto suave e persistente, sem translação: origem e destino crescem discretamente ao redor do centro da geometria territorial individual, nunca ao redor do centro da máscara ou do mapa, até confirmação, cancelamento, invalidação ou mudança de fase. O root lógico, o collider e o contador permanecem imóveis. Esse estado é visualmente distinto da conquista territorial comum, que usa somente glow temporário sem mover o território.

Quando uma mudança canônica de proprietário completa pela primeira vez o controle individual de todos os territórios de uma região definida em `DefinicaoMapa.Regioes`, a resolução emite um evento visual regional separado. O efeito coordena simultaneamente os territórios membros com glow mais forte e leve escala visual durante aproximadamente 1,5 segundo. A detecção compara ownership anterior e posterior, não usa cor, tropas, Energy ou o overlay informativo e não se repete enquanto a região já estiver integralmente controlada.

O ciclo real de rounds avança cooldown uma única vez por round. A consulta runtime não executa armas nem altera territórios. A composição autoritativa dos decks Normais e os níveis efetivos são capturados em snapshots por participante; Random Cards usa diretamente os IDs canônicos e níveis capturados, sem fallback silencioso fora das regras temporárias explicitamente aprovadas.

### Identidade e loadout da partida — decisão definitiva

A Central de Energia é uma arma-base universal: integra o catálogo oficial de 26 armas, não ocupa os oito slots principais nem os dois slots de Apoio do deck Normal e não participa do sorteio Random Cards. Seu custo zero permite uso desde o Round 1, mas sua produção começa somente no Round 2. Quando possuída, usa o nível permanente capturado; quando ainda não possuída, recebe N1 temporário apenas naquela partida, sem alterar a progressão.

A produção de Energy da Central ocorre no início dos três rounds seguintes à instalação (N1 +5, N2 +10, N3 +15 e N4 +20 por pagamento). Além dela, cada ataque terrestre que obtém sucesso efetivo segundo o `ResultadoAcaoTerrestre` canônico acumula +5 Energy para a Preparation do round seguinte. Tentativa fracassada, movimento, transferência e ocupação de território VAZIO sem combate não geram Energy. Os créditos pendentes de ataques e os pagamentos de Central são somados uma única vez na entrada da Preparation correspondente; somente então alteram o runtime autoritativo e atualizam imediatamente o indicador de Energy e a disponibilidade das cartas. Território e região não geram Energy: regiões completas concedem exclusivamente o bônus de tropas definido em `DefinicaoMapa.Regioes`.

O Random Cards utiliza diretamente `WeaponId` canônico. Seu pool efetivo contém as 25 armas oficiais exceto `energy_plant`. Os antigos `card_01` a `card_24` eram placeholders derivados dos três decks mock de oito cards, não possuíam correspondência semântica e deixam de ser identidade funcional. Saves locais que ainda os contenham têm esses slots limpos, sem conversão inventada.

O pool BASIC do Round 1 contém exatamente `air_strike`, `reinforcements`, `minefield`, `blitzkrieg`, `transfer_nullifier`, `sniper`, `air_transport` e `bunker`. A quantidade inicial, a sequência comum determinística, as entregas posteriores e a capacidade da mão permanecem inalteradas.

No Random Cards, uma arma possuída usa o nível permanente capturado no início da partida; uma arma não possuída usa N1 temporário. No modo Normal não existe esse fallback: cada participante recebe um snapshot imutável próprio com `DeckId`, oito armas principais, dois Apoios, IDs canônicos e níveis provenientes de `WDWeaponProgress`. Dados ausentes, bloqueados ou corrompidos produzem estado técnico explícito. Mudanças posteriores no perfil não alteram o snapshot da partida.

### Etapa 7C — programação durante a Preparação

A programação de armas é uma camada de intenção separada da execução. Durante a Preparação, o jogador escolhe uma arma disponível e fornece origem, alvo, instalação e parâmetros conforme um contrato técnico canônico. O serviço consulta o loadout/mão, nível, Energy, cooldown, usos e a visão read-only do tabuleiro, mas não chama handlers nem o pipeline de resolução.

Criar, cancelar ou substituir uma intenção não debita Energy, não inicia cooldown, não consome carta e não altera tropas, proprietário ou instalações. Instalar Drone/Foguetes e disparar uma instalação existente são intenções distintas.

Ao fechar a Preparação, as intenções daquele round são congeladas em uma coleção imutável e ordenadas por round, fase, ordem oficial do jogador, ordem de programação e `ActionId`. A futura etapa de execução deverá revalidar cada ação contra o estado autoritativo daquele momento, pois uma intenção válida na Preparação pode deixar de ser válida durante a resolução.

### Etapa 7D.1 — porta autoritativa de mutação

O estado real da partida permanece a única fonte de verdade para territórios. A porta `IWDGameWeaponMutationPort` valida comandos técnicos e aplica transições completas diretamente no território real; o `WDGameWeaponContext` continua read-only e reflete a alteração na consulta seguinte, sem cópia concorrente do tabuleiro.

As transições territoriais garantem invariantes finais: Ocupado possui jogador canônico e pelo menos uma tropa; Neutro não possui dono e tem exatamente uma tropa; Vazio não possui dono e tem zero tropas. Dano que zera um território conclui a operação em Vazio. Transferência troca o proprietário do mesmo `TerritoryId` preservando as tropas e não depende de adjacência.

Energy, cooldown e usos são alterados exclusivamente no `WDWeaponMatchRuntime`. Instalações usam um único `WDWeaponInstallationRegistry` runtime indexado por IDs canônicos e território, sem GameObjects visuais nem lista concorrente no `GameManager`. Cada mutação pode receber `MutationId`; um ID aplicado com sucesso não pode ser aplicado novamente. Operações validam todos os pré-requisitos antes de escrever, preparando comandos compostos futuros sem deixar estado parcial.

Os handlers das 25 armas e o `WDWeaponResolutionPipeline` continuam desligados da partida. As ações congeladas da Preparação não são executadas nesta etapa.

### Etapa 7D.2 — primeiro lote executando no estado real

A integração incremental ativa somente quatro armas: Reforços na Fase 5 e Bunker,
Flak Tower e Central de Energia na Fase 6. O `WDWeaponExecutionCoordinator` recebe as
ações congeladas, revalida disponibilidade e alvo no contexto vivo, aplica o efeito
pela mutation port e somente então compromete Energy, cooldown e uso. Repetir o mesmo
`ActionId` não duplica efeito nem custo. As outras 21 armas permanecem adiadas, sem
execução e sem cobrança; o `WDWeaponResolutionPipeline` global continua desligado.

Reforços adiciona diretamente ao território real próprio ou aliado a quantidade
canônica do nível. Bunker registra barreira persistente e Flak registra nível, chance
derivada e três rounds de duração no `WDWeaponInstallationRegistry`. Nesta etapa elas
não possuem representação visual final; absorção de dano e interceptação ainda não
estão conectadas ao gameplay.

A Central universal pode ser instalada no Round 1 com custo zero, registra cooldown e
uso e mantém três pagamentos. A Fase 0 paga ao proprietário atual nos três rounds
seguintes à instalação, nunca no round da instalação e nunca mais de uma vez no mesmo
round. O pagamento altera a Energy real do `WDWeaponMatchRuntime`; captura e abandono
continuam representáveis pelo registro autoritativo, sem fluxo artificial nesta etapa.

### Etapa 7D.3 — lifecycle e primeiro lote ofensivo real

A integração real passa a abranger 10 das 25 armas: Reforços, Bunker, Flak Tower,
Central de Energia, Ataque Aéreo, Bomba de Longo Alcance, Bomba Química, Kamikaze,
Foguetes e Artilharia. As outras 15 permanecem `Deferred`, sem efeito ou custo, e o
`WDWeaponResolutionPipeline` global continua desligado.

O lifecycle autoritativo remove Bunker e Flak quando o território vira Vazio e abandona
Central, Foguetes e Artilharia, preservando seus recursos restantes. Captura sem passar
por Vazio transfere a propriedade das instalações sem restaurar HP, duração, tiros,
operações ou pagamentos. A Flak protege seu território e vizinhos próprios/aliados por
exatamente três rounds de existência, escolhe uma única cobertura por maior nível e ID
estável e usa resolução aleatória determinística.

O dano real usa a mutation port: Bunker absorve dano convencional antes das tropas,
Bomba Química ignora sua barreira, e tropa zerada produz território Vazio sem conquista.
Kamikaze sacrifica a tropa no lançamento mesmo quando interceptado. Foguetes guarda dois
disparos e cobra a instalação, não cada tiro. Artilharia escolhe automaticamente até três
vizinhos atacáveis por tropas decrescentes e ID, consumindo operação somente quando há
disparo. Instalações continuam sem representação visual definitiva.

### [DEFINITIVO] Ordem global de resolução das armas — V2

#### Fase 0 — Início do round

- produção automática das Centrais de Energia já ativas;
- parcelas automáticas pendentes de Mercenários;
- carregamento dos dados necessários do round anterior.

#### Fase 1 — Paz

#### Fase 2 — Anulador de Transferência

#### Fase 3 — Transferências de território

#### Fase 4 — Interferências

- Sabotagem;
- Infiltrado.

#### Fase 5 — Reforços e distribuição

- reforço natural por territórios e regiões, reduzido por Sabotagem quando aplicável;
- Reforços;
- Tropas Reservas;
- Recuperação de Tropas;
- Mercenários, incluindo a primeira parcela da ativação;
- distribuição das tropas disponíveis.

#### Fase 6 — Instalações e logística

- Campo Minado;
- Bunker;
- Flak Tower;
- Artilharia;
- Foguetes;
- Central de Energia;
- Transporte Aéreo.

Central recém-instalada produz somente a partir do round seguinte. Artilharia e Foguetes recém-instalados já podem operar no round atual conforme suas regras.

#### Fase 7 — Sniper

#### Fase 8 — Propaganda de Guerra

#### Fase 9A — Armas ofensivas programadas

- Ataque Aéreo;
- Drone;
- Bomba Atômica;
- Bomba de Longo Alcance;
- Bomba Química;
- Kamikaze;
- Foguetes.

#### Fase 9B — Artilharia automática

#### Fase 10 — Paraquedistas

#### Fase 11 — Ações terrestres

Movimentos para território próprio, aliado, inimigo ou Vazio pertencem ao mesmo limite de ações terrestres.

#### Fase 12 — Consolidação

O servidor consolida proprietários, capturas, instalações, minas, durações, tiros restantes, baixas, territórios perdidos, dados para Tropas Reservas e Recuperação, cooldowns, Energia, usos e o estado necessário ao próximo round.

### [DEFINITIVO] Regras técnicas da resolução

- O servidor/backend futuro é a autoridade final.
- A ordem é determinística e cada ação revalida o estado no momento apropriado.
- Toda ação possui ID único e tratamento idempotente.
- O servidor não confia no cliente para Energia, cooldown, alvo, resultado, instalação, tropas ou propriedade.
- Efeitos, custos, anulações, falhas, interceptações e alterações de estado são auditáveis.

### [HISTÓRICO — SUPERADO PELA IMPLEMENTAÇÃO CANÔNICA 7D.7A] Especificação de implementação e balanceamento

As regras documentais não autorizam ainda a implementação completa das armas. Antes disso, deve-se transformar a matriz consolidada em especificação técnica, verificar as interações das 25 armas e revisar comparativamente Energia, cooldowns, níveis, duração, usos e força relativa. As curvas padrão de progressão em Tokens específicos e Moedas já estão definidas; exceções futuras exigem decisão explícita.

### [HISTÓRICO — STATUS EM 08/09/2026, SUPERADO PELO CATÁLOGO DE 26 ARMAS] Revisão das armas

- Catálogo nominal: 25 armas.
- A revisão conceitual inicial das 25 armas foi concluída.
- Nenhuma mecânica específica das armas deve ser inferida além da infraestrutura compartilhada autorizada.
- Custos de Energia ainda passarão por revisão comparativa global.
- Ordem global V2 está definida como regra de resolução.
- A Matriz de Interações V2 registra as regras de produto atuais; sua especificação técnica exaustiva ainda deve ser produzida antes da implementação.
- Curvas padrão de Tokens específicos e Moedas estão definidas para armas de 1, 4 e 6 níveis.

### [FUTURO] Backlog de armas além do catálogo V1

O núcleo deve aceitar novas armas sem presumir que o catálogo permanecerá limitado a 25. Backlog apenas documental/configuracional, sem implementação autorizada nesta etapa: Tropas de Resistência, Barricada, Roubo de Movimento, Espião, Muralha, Emboscada, Bloqueio de Fronteira, Contraespionagem, Isca / Decoy, Interferência de Comunicações e Contra-Ataque.

## 9. Economia, VIP, administração e segurança

### [DEFINITIVO] Recursos econômicos

- **Moedas** são um recurso global da conta. Servem futuramente para upgrades em conjunto com os Tokens próprios de cada arma e podem vir de vitórias, missões, recompensa diária, baús, eventos e anúncios recompensados voluntários. Pacotes de Moedas poderão ser adquiridos com Esmeraldas. Fontes, quantidades, preços e limites permanecem configuráveis e ainda não estão balanceados.
- **Tokens** pertencem sempre a uma arma específica; não existe saldo universal de Tokens. Podem continuar acumulando quando a arma atingir o nível máximo e poderão participar futuramente de doações de clã. Não converter, descartar ou redistribuir Tokens automaticamente sem nova decisão de produto.
- **Esmeraldas** são o recurso premium. Poderão ser adquiridas com dinheiro real e obtidas em pequenas quantidades por recompensas limitadas. Usos futuros incluem pacotes de Moedas, alteração de nickname, personalização e entrada em eventos. Valores e preços ainda não estão definidos.

A interface não determina saldo, preço, recompensa ou elegibilidade. A autoridade futura desses dados será o backend, com operações transacionais e auditáveis.

### [DEFINITIVO] Recompensas, retenção e limites

- Baús podem conter Moedas, Tokens específicos por arma e, ocasionalmente, pequena quantidade de Esmeraldas. Não incluem skins nem avatares nesta definição. Oportunidades normais e VIP, cooldowns, limites e uma abertura extra opcional por anúncio recompensado devem ser configuráveis e impedir farm infinito.
- A recompensa diária usa um ciclo de sete dias consecutivos. Dias comuns podem entregar Moedas e Tokens; completar o sétimo dia pode entregar Esmeraldas. Perder um dia reinicia o ciclo. Um bônus VIP futuro pode existir, mas precisa ser moderado e configurável.
- Missões não exigem vencer a partida. Concedem badge permanente e podem conceder Moedas ou Tokens.
- Torneios podem premiar Moedas, Tokens ou Esmeraldas aos três primeiros colocados. Benefícios adicionais do campeão permanecem futuros.
- Anúncios são sempre recompensados e voluntários. Não há autorização atual para anúncios obrigatórios ou intersticiais.
- Ofertas rotativas ou diárias devem ser data-driven, mas permanecem inativas até uma implementação própria.

### [DEFINITIVO] VIP sem pay-to-win

O VIP é planejado para períodos de três, seis e doze meses, com preços ainda indefinidos. Seus benefícios incluem cinco decks persistentes em vez de dois, peso de voto 2, oportunidades moderadas de baús/recompensas, cosméticos, reações, molduras e conveniências relacionadas a anúncios.

VIP pode acelerar progressão, mas nunca eleva o teto competitivo: não concede tropas, ataques, força superior no mesmo nível nem conteúdo de poder exclusivo no nível máximo. A expiração não apaga decks ou progresso. A administração futura poderá conceder ou remover VIP, inclusive permanente, sem depender de compra.

### [DEFINITIVO] Administração e separação de responsabilidades

O menu do Unity é ferramenta de desenvolvimento para configurações globais de produto. Dados vivos de contas não serão administrados pelo cliente público nem por comandos secretos nele embutidos. Um futuro painel administrativo autenticado no backend poderá:

- conceder ou remover VIP, inclusive permanente;
- corrigir recursos e troféus;
- suspender ou banir contas;
- excluir ou anonimizar dados conforme as regras aplicáveis;
- administrar jogadores, clãs, torneios, denúncias e recompensas futuras.

Operações sensíveis exigem autenticação, papéis/permissões, motivo, horário, estado anterior e posterior e registro de auditoria. O painel deve operar sobre serviços autoritativos, não editar diretamente dados locais do cliente.

### [FUTURO] Analytics operacional

O painel administrativo poderá apresentar histórico, gráficos, percentuais, filtros e drill-down de jogadores e clãs. Esses dados virão de eventos e transações server-side; totais calculados apenas pelo cliente não são fonte confiável.

### [DEFINITIVO] Autoridade e proteção contra abuso

- Economia, progresso, inventário, decks visíveis, timers e entitlement VIP serão autoritativos no servidor.
- Doações futuras de Tokens terão cooldown, vínculo de conta, transações atômicas e idempotentes.
- Recompensas de partidas exigirão validação de identidade, resultado e telemetria, com detecção de repetição e conluio.
- Desconexão não pode congelar ou reiniciar timers autoritativos.
- VIP terá estados explícitos, como pendente, ativo, expirado e cancelado, com reconciliação de compra.
- Ações futuras de armas usarão identificador único, rejeição de replay, contadores de uso determinísticos e trilha de auditoria.
- **Idempotência é regra transversal:** repetir a mesma requisição não pode duplicar compra, recompensa, upgrade, doação ou efeito de gameplay.

### [PROVISÓRIO] Persistência local e configuração de desenvolvimento

`IWDArmoryRepository` já separa o domínio do mecanismo de persistência. `WDPlayerPrefsArmoryRepository` é apenas o adaptador local de desenvolvimento e deverá ser substituído por uma implementação de conta/backend sem acoplar a UI ao armazenamento.

Na terminologia de produto e UI, esta área se chama **ARSENAL**. Os identificadores técnicos históricos `WDArmory*` permanecem inalterados para evitar uma renomeação cosmética sem benefício arquitetural.

O catálogo real de Armas e Progressão pode ser editado em **War Dominion > Configurações do Jogo > Armas e Progressão**, que abre o mesmo `WDArmoryCatalogConfig.asset` consumido pelo runtime. Não existe ferramenta vazia para economia ou administração ao vivo: novos acessos só devem aparecer quando houver configuração ou serviço real para editar.

## 10. HUD de partida

### [DEFINITIVO] Estrutura desejada

- O mapa mantém prioridade visual.
- Não existe TopBar permanente como solução final.
- Informações essenciais ficam nas bordas.
- Informações secundárias aparecem sob demanda.
- Controles devem ser compactos, gráficos e iconográficos.
- Jogadores e times podem expandir informações quando necessário.
- Regiões e bônus devem ser apresentados de forma contextual.
- O chat deve existir como painel, evitando ocupar permanentemente espaço central.

### [PROVISÓRIO] Implementação atual

Permanecem funcionais, reutilizáveis e ainda não aprovados visualmente como resultado final:

- `WarDominionMatchHUD`;
- `WDUIPremiumComponents`;
- `PreparedActionArrowView`;
- presenters visuais de resolução;
- aparência de molduras;
- aparência de botões;
- tipografia;
- contadores;
- setas;
- feedbacks visuais.

A infraestrutura funcional desses elementos pode ser preservada enquanto a apresentação é substituída ou refinada no passe premium.

## 11. Preparação e Resolução

### [DEFINITIVO] Separação semântica

- **Preparação** comunica intenção, escolha e planejamento.
- **Resolução** comunica execução, impacto e consequência.

As duas fases não devem compartilhar a mesma intensidade visual nem produzir ambiguidade sobre quando o resultado lógico já ocorreu.

### [DEFINITIVO] Preparação

Durante a Preparação, a interface deve permitir compreender e revisar ações antes do envio, incluindo seleção, distribuição, remoção, transferência e indicação visual das intenções preparadas conforme os contratos funcionais disponíveis.

### [DEFINITIVO] Ataque preparado

O diamante/pulso de feixe anteriormente experimentado foi rejeitado como direção final.

Para evolução visual:

- o contador de origem pode reagir ou pulsar;
- a seta deve evoluir para uma forma curva e premium;
- a visualização deve considerar a cor e a energia do jogador;
- o efeito deve comunicar intenção, não impacto já consumado.

### [DEFINITIVO] Resolução

A Resolução deve ser mais intensa e pode empregar, quando adequado:

- trajetória e energia;
- impacto e flash;
- partículas;
- fumaça;
- fogo quando fizer sentido;
- transição visual de controle;
- reação do contador;
- dissipação e encerramento legíveis.

### [DEFINITIVO] Autoridade lógica

A camada visual nunca decide gameplay. `GameManager` e os resultados lógicos são a autoridade. Presenters e sequências exibem estados e consequências já determinados, podendo usar estado visual temporário sem substituir o estado autoritativo.

## 12. Transferência territorial

### [DEFINITIVO] Regra funcional

Transferência é a entrega completa do controle de um território a um aliado, preservando as tropas existentes nesse território. Não é movimentação de tropas.

No modelo 2v2 atual há apenas um aliado elegível, portanto não é necessário perguntar o destinatário: ao escolher o território a entregar, a transferência é programada diretamente, sem confirmação adicional. No 3v3, o jogador primeiro escolhe um dos dois aliados em popup compacto; depois escolhe o território a entregar àquele destinatário, também sem confirmação adicional. Fechar o popup cancela o fluxo sem consumir a transferência, e escolher um território não troca automaticamente o aliado selecionado. A arquitetura deve continuar extensível para outros formatos.

Concluir ou cancelar o targeting de transferência limpa o destaque transitório de seleção. Uma transferência já programada permanece identificável pela lista de ações, mas não mantém glow territorial concorrente durante o restante da Preparation.

### [DEFINITIVO] Regra visual

Uma transferência não deve mostrar:

- tropas viajando;
- rota origem-destino;
- quantidade em trânsito;
- explosão;
- impacto ofensivo;
- linguagem de combate.

Ela deve comunicar passagem amigável de controle no próprio território:

- estado/proprietário anterior;
- destaque de transição;
- novo proprietário e nova cor no momento correto;
- tropas preservadas conforme o resultado lógico;
- reação discreta do contador.

## 13. Mapas oficiais e vizinhanças

### [DEFINITIVO] Mapas oficiais

Os mapas oficiais atuais são:

- Classic;
- Pearl Harbor;
- Batalha do Riachuelo;
- Peloponeso;
- Dark World.

`MapaTeste12` foi removido e não integra mais o conjunto de mapas do produto.

### [DEFINITIVO] Regra de adjacência

- Territórios com fronteira terrestre compartilhada podem ser vizinhos.
- Territórios sem fronteira terrestre compartilhada exigem ponte, linha, caminho ou conector explícito definido pelo mapa.
- Toda conexão deve ser bidirecional.

## 14. Arte oficial, gabaritos e geometria

### [DEFINITIVO] Autoridades por camada

- A arte oficial é a camada visual que permanece visível.
- O gabarito branco 1:1 é a autoridade geométrica dos territórios.
- Máscaras e `PolygonCollider2D` sustentam interação e permanecem invisíveis no jogo final.
- Referências coloridas antigas servem somente como apoio semântico; não devem ser usadas para reconstruir geometria.

### [DEFINITIVO] Remaster visual sem mudança geométrica

Quando o trabalho for exclusivamente visual, devem ser preservados:

- resolução;
- registro/alinhamento;
- posições;
- proporções;
- fronteiras;
- geometria territorial.

Uma atualização visual não deve exigir máscaras ou colliders novos se a geometria oficial não mudou.

## 15. Remaster futuro de mapas

### [FUTURO] Classic

Um remaster mais profundo do Classic pode incluir nova arte, nova interpretação dos continentes e mudanças de fronteiras ou composição. Isso não bloqueia a evolução atual do produto.

Se as fronteiras mudarem, trata-se de nova geometria. Nesse caso:

- não reutilizar cegamente máscaras antigas;
- criar gabarito correspondente;
- gerar novas máscaras e colliders;
- revisar adjacências;
- revisar contadores;
- executar validação completa do mapa.

### [FUTURO] Dark World

O Dark World poderá seguir uma de duas direções, a decidir futuramente:

- **Opção A:** remaster preservando a geometria existente;
- **Opção B:** reconstrução profunda ou nova versão, com geometria e validações próprias.

Nenhuma dessas opções deve ser iniciada apenas por estar registrada neste documento.

## 16. Tabela de decisões

### Decisões definitivas

| DECISÃO | STATUS | OBSERVAÇÃO |
|---|---|---|
| Separar implementação funcional de aprovação visual | [DEFINITIVO] | Funcional no Play não significa visual aprovado. |
| Trabalhar a UI em dois passes | [DEFINITIVO] | Fundação global primeiro; polimento premium depois. |
| Diferenciar Home e HUD de partida | [DEFINITIVO] | Home é mais rica; HUD preserva protagonismo do mapa. |
| Remover TopBar permanente do HUD final | [DEFINITIVO] | Informações essenciais ficam nas bordas e as secundárias aparecem sob demanda. |
| Usar glow de forma controlada | [DEFINITIVO] | Menor na Preparação e maior na Resolução. |
| Usar packs externos como matéria-prima | [DEFINITIVO] | Nenhum pack define isoladamente a identidade. |
| Tornar PLAY protagonista da Home | [DEFINITIVO] | Progressão competitiva e recursos sociais se organizam ao redor desse foco. |
| Preservar fundo visível e composição não preenchida na Home | [DEFINITIVO] | Cards variam de escala conforme importância; PLAY não deve se tornar um retângulo simples gigantesco. |
| Abrir JOGAR PARTIDA diretamente por PLAY | [DEFINITIVO] | Não existe etapa intermediária com JOGAR, MODOS e MAPAS. |
| Administrar disponibilidade de modos por configuração | [DEFINITIVO] | Available, ComingSoon e Hidden não dependem de mudanças na UI nem removem a implementação funcional. |
| Administrar disponibilidade global de mapas | [DEFINITIVO] | Um mapa desabilitado permanece no projeto, mas não entra em seleção ou votação. |
| Centralizar compatibilidade modo × mapa | [DEFINITIVO] | Cada modo usa política All, Allow List ou Deny List, sempre combinada com a disponibilidade global do mapa. |
| Separar idioma de fuso horário | [DEFINITIVO] | O idioma da interface e o identificador de timezone são preferências independentes e persistíveis. |
| Usar matchmaking futuro unificado e parametrizado | [DEFINITIVO] | Modos não recebem arquiteturas de matchmaking independentes. |
| Configurar elegibilidade e peso de mapas explicitamente | [DEFINITIVO] | Quantidade de territórios não é critério codificado pela UI. |
| Votar em mapas por 15 segundos | [DEFINITIVO] | Até dois candidatos elegíveis, abstenção válida e desempate somente entre empatados. |
| Identificar Guest com quatro dígitos sem presumir bot | [DEFINITIVO] | `GuestXXXX` também pode pertencer a jogador real. |
| Adotar 10 ligas seguidas de Veteran | [DEFINITIVO] | Veteran tem progressão prática sem limite fechado. |
| Rejeitar pay-to-win | [DEFINITIVO] | Monetização não concede vantagem competitiva comprável. |
| Separar Moedas, Tokens por arma e Esmeraldas | [DEFINITIVO] | Recursos têm funções distintas; valores e balanceamento permanecem configuráveis. |
| Manter VIP abaixo do teto competitivo | [DEFINITIVO] | Pode acelerar e oferecer conveniência/cosméticos, mas não concede poder máximo superior. |
| Tornar backend autoritativo para economia e progressão | [DEFINITIVO] | Cliente local não valida saldos, recompensas, timers ou entitlement. |
| Exigir idempotência e auditoria | [DEFINITIVO] | Repetições não duplicam operações e ações administrativas sensíveis registram antes/depois. |
| Separar intenção de execução | [DEFINITIVO] | Preparação comunica plano; Resolução comunica consequência. |
| Manter o resultado lógico como autoridade | [DEFINITIVO] | A apresentação visual não decide gameplay. |
| Tratar transferência como passagem de controle | [DEFINITIVO] | Território inteiro passa ao aliado e preserva tropas. |
| Manter cinco mapas oficiais | [DEFINITIVO] | Classic, Pearl Harbor, Riachuelo, Peloponeso e Dark World. |
| Exigir conectores explícitos sem fronteira terrestre | [DEFINITIVO] | Conexões são bidirecionais. |
| Separar autoridade visual e geométrica dos mapas | [DEFINITIVO] | Arte oficial é visível; gabarito 1:1 governa geometria. |

### Implementações provisórias

| DECISÃO | STATUS | OBSERVAÇÃO |
|---|---|---|
| HUD atual da partida | [PROVISÓRIO] | A infraestrutura funciona, mas o design visual final não está aprovado. |
| Componentes premium atuais | [PROVISÓRIO] | `WDUIPremiumComponents` é fundação, não acabamento definitivo. |
| Setas de ações preparadas | [PROVISÓRIO] | `PreparedActionArrowView` deverá receber direção visual premium. |
| Presenters da Resolução | [PROVISÓRIO] | Contratos e sequência são reutilizáveis; aparência ainda evoluirá. |
| Molduras, botões, tipografia, contadores e feedbacks atuais | [PROVISÓRIO] | Devem ser avaliados no passe de polimento premium. |
| Dimensionamento dos cards da Home na Passada 1 | [PROVISÓRIO] | Valida arquitetura e fluxo, não a composição visual definitiva. |
| Fluxos locais de seleção e matchmaking | [PROVISÓRIO] | Validam navegação; não executam rede, servidor, cartas ou matchmaking. |
| Votação local de mapas | [PROVISÓRIO] | Executa seleção, voto, contagem e resultado sem sincronização multiplayer. |
| Administração local de conteúdo e preferências regionais | [PROVISÓRIO] | Modos, mapas, compatibilidade, idioma e timezone são configurados localmente; backend futuro poderá fornecer os mesmos contratos. |

### Decisões pendentes

| DECISÃO | STATUS | OBSERVAÇÃO |
|---|---|---|
| Valores e balanceamento da economia | [PARCIAL] | Fontes e usos estão definidos; quantidades, preços, cooldowns e limites ainda serão balanceados. |
| Preços e intensidade dos benefícios VIP | [PARCIAL] | Durações e limites competitivos estão definidos; preços e magnitudes econômicas permanecem pendentes. |
| Modelo da Store | [PENDENTE] | Não definido. |
| Detalhes de monetização | [PENDENTE] | Devem respeitar a regra contra pay-to-win. |
| Catálogo de missões | [PENDENTE] | Não definido. |
| Limites e faixas das ligas | [PENDENTE] | A estrutura geral existe; os thresholds ainda não. |
| Regras de matchmaking | [PENDENTE] | Devem impedir confronto inadequado entre veteranos e iniciantes. |
| Tamanho da partida FFA | [PENDENTE] | Não foi definido e não deve ser inferido pela interface. |

### Evoluções futuras

| DECISÃO | STATUS | OBSERVAÇÃO |
|---|---|---|
| Painel administrativo e analytics server-side | [FUTURO] | Administração de contas e métricas exige autenticação, papéis, auditoria e dados autoritativos. |
| Cosméticos e progressão VIP | [FUTURO] | Detalhar conteúdo respeitando o mesmo teto competitivo para todos. |
| Anúncios recompensados voluntários | [FUTURO] | Podem conceder benefício limitado; anúncios obrigatórios não estão autorizados. |
| Outras receitas compatíveis | [FUTURO] | Exigem definição de produto. |
| Remaster profundo do Classic | [FUTURO] | Pode exigir nova geometria se fronteiras mudarem. |
| Remaster ou reconstrução do Dark World | [FUTURO] | Direção A ou B ainda será escolhida. |
| 1x1 Deckbuilder | [FUTURO] | Quatro escolhas em pares formam decks de oito cartas. |
| Battle Royale | [FUTURO] | Seis jogadores, cinco rounds e Deckbuilder. |
| Bots estratégicos e adaptativos | [FUTURO] | Aprendem apenas com informação observável e possuem limites balanceáveis. |
| Integração das Armas de Apoio à partida | [FUTURO] | Dois slots adicionais, um uso independente por Apoio; efeitos concretos permanecem pendentes. |
| Localização integral da interface | [FUTURO] | Idiomas planejados: português, inglês, espanhol e italiano; a arquitetura também aceita alemão sem acoplar idioma ao fuso horário. |

## 17. Configuração de conteúdo e preferências regionais

### [DEFINITIVO] Disponibilidade administrativa

Modos e submodos possuem estado configurável `Available`, `ComingSoon` ou `Hidden`.
`ComingSoon` mantém a implementação funcional disponível internamente, mas impede o
acesso público; `Hidden` também omite a opção da interface. Mapas possuem habilitação
global independente. Desabilitar conteúdo não apaga cenas, assets ou gameplay.

### [DEFINITIVO] Compatibilidade modo × mapa

A elegibilidade final de um mapa combina sua habilitação global com a política do modo:
`All`, `AllowList` ou `DenyList`. A decisão é centralizada e data-driven; a UI e a
votação não devem introduzir condições específicas por ID. As listas locais atuais
preservam o comportamento funcional existente. No futuro, um backend poderá fornecer
os mesmos dados sem mudar o contrato consumido pelos fluxos.

### [DEFINITIVO] Idioma e timezone independentes

Idioma de interface e timezone são preferências distintas. A fundação contempla
português, inglês, espanhol e italiano e já aceita alemão. O timezone usa identificador
de zona da plataforma, com detecção automática local e suporte a seleção manual futura,
em vez de armazenar apenas um deslocamento fixo como `UTC-3`. Eventos guardam um
instante absoluto e são convertidos centralmente para o timezone do jogador.

Nesta fase, defaults e preferências permanecem locais e configuráveis. Uma futura tela
de Configurações chamará os serviços existentes para alterar idioma, escolher timezone
automático ou manual e converter horários, sem misturar as duas escolhas. Traduções,
tela de Configurações e integração com backend não fazem parte desta fundação.

## 18. Editor de Mapas interno

### [PROVISÓRIO] V1 do Editor de Mapas

O War Dominion possui uma janela Unity Editor própria e dockável para manutenção dos
mapas atuais e criação de novas definições. A fonte autoritativa permanece formada por
`DefinicaoMapa`, `CatalogoMapas`, sprites, máscaras, colliders e dados Unity existentes;
não existe JSON paralelo governando o jogo.

Os módulos da V1 são Mapa, Arte, Territórios, Regiões, Conexões, Contadores,
Thumbnail e Validação. O viewport oferece zoom e pan apenas de trabalho, camadas
independentes e inspeção de conexões/posições sem alterar o enquadramento real.

- conexões são cadastradas e removidas bidirecionalmente, com Undo;
- a arte principal pode ser substituída preservando os dados, sempre com aviso sobre
  compatibilidade geométrica;
- geometria automática reutiliza os geradores 1:1 existentes como pipeline assistido,
  sem destruir máscaras silenciosamente;
- thumbnail automático permanece assistido na V1 até existir referência autoritativa
  própria em `DefinicaoMapa`;
- o validador cobre identidade, arte, territórios, regiões, bônus, referências,
  duplicações, bidirecionalidade, isolamento e posições de contadores;
- a separação entre operações, validação e `EditorWindow` prepara reutilização futura
  da lógica em um editor standalone, sem implementar o executável agora.

## 19. Manutenção deste documento

### [DEFINITIVO] Regra de atualização

Toda decisão estrutural de produto ou design deve atualizar este documento no mesmo ciclo de trabalho em que for aprovada.

Ao atualizar:

- alterar o status quando uma pendência se tornar decisão;
- registrar substituições de direção, sem manter regras contraditórias como se ambas fossem atuais;
- preservar contexto suficiente para distinguir infraestrutura funcional de aparência aprovada;
- manter tabelas e seções coerentes entre si;
- não inventar detalhes para preencher itens ainda pendentes;
- tratar a versão corrente do documento como referência ativa para novas fases.

## Armas V1 — Etapa 7D.4 acelerada (histórico da integração incremental)

[HISTÓRICO — SUPERADO PELA 7D.7A]

- 18 das 25 armas V1 estão conectadas ao estado real da partida.
- Este lote integra Bomba Atômica, Drone, Sniper, Propaganda de Guerra, Sabotagem,
  Anulador de Transferência, Blitzkrieg e Tropas Reservas.
- Um runtime autoritativo por round concentra bloqueios temporários, modificadores de
  reforço/ações terrestres, pool independente de reforços e histórico idempotente de
  perdas territoriais. O reconhecimento do Drone descrito nesta etapa foi superado
  pela semântica canônica Drone ofensivo + Radar da Etapa 7D.7A.
- Sabotagem reduz somente o reforço natural e usa apenas a maior porcentagem válida.
- Tropas Reservas deriva seu valor das perdas do round anterior e entra no pool de
  reforços; não é reduzida por Sabotagem.
- Sniper bloqueia a origem atingida para ações terrestres, Foguetes e Artilharia no
  round; disparos/operações bloqueados não são consumidos.
- Propaganda usa a quantidade atual de tropas, neutraliza com `floor(tropas / 2)` e
  mínimo 1. Instalações não recebem lifecycle novo enquanto estiverem em Neutro.
- A Bomba Atômica congela os vizinhos antes das mutações; somente a Flak central pode
  cancelar a explosão inteira, e cada território aplica Bunker/lifecycle separadamente.
- A antiga operação de reconhecimento do Drone foi removida na Etapa 7D.7A; a regra
  vigente é a instalação ofensiva com dois disparos, enquanto Radar concentra a
  revelação de cobertura Flak.
- Anulador bloqueia a transferência antes da mutação sem consumir a oportunidade do
  alvo; somente o primeiro efeito válido contra o jogador é cobrado.
- Blitzkrieg mantém o limite-base 3 e fornece, por round, os totais 4/5/6/7.
- Campo Minado, Paz, Transporte Aéreo, Paraquedistas, Infiltrado, Recuperação de Tropas
  e Mercenários permanecem `Deferred`, sem custo, cooldown, uso ou mutação.
- O `WDWeaponResolutionPipeline` global permanece desligado; a integração continua
  controlada pelo coordenador incremental e não possui apresentação visual definitiva.
## Armas V1 — Etapa 7D.5 acelerada (histórico do último lote individual)

[HISTÓRICO — SUPERADO PELA 7D.7A]

- As 25 armas canônicas possuem integração individual com o estado autoritativo.
- Paz, Campo Minado, Transporte Aéreo, Paraquedistas, Infiltrado, Recuperação de
  Tropas e Mercenários deixam de ser `Deferred` por ausência de integração.
- Paz é simétrica, temporária por round e bloqueia ações hostis aplicáveis; a ação
  recíproca não cobra Energy, cooldown ou uso. Collateral atômico continua geográfico.
- Campo Minado é uma instalação persistente e secreta, permanece em território Vazio,
  não é transportável e pode ser desarmada temporariamente por Infiltrado.
- Transporte Aéreo move tropas entre território próprio e próprio/aliado, deixa uma
  guarnição e pode transportar somente Flak, preservando nível e duração.
- Paraquedistas revalidam o alvo sequencialmente, sofrem Flak/Bunker/Mina e são a única
  arma autorizada a ocupar/conquistar território.
- A antiga infiltração territorial deste lote foi substituída na Etapa 7D.7A pelo
  bloqueio determinístico de uma ação de arma preparada, identificada por `ActionId`.
- Recuperação usa baixas reais do round anterior, somente quando o território manteve
  o mesmo proprietário, limitada pelo nível.
- Mercenários paga três parcelas no pool autoritativo (ativação, round +1 e round +2),
  sem nova cobrança nas parcelas automáticas.
- A lacuna de produto para instalações em território tornado Neutro por Propaganda
  permanece aberta; nenhum lifecycle não aprovado foi inventado.
- O `WDWeaponResolutionPipeline` global continua desligado. UI/VFX e ativação global
  da Resolution Order V2 permanecem para etapa posterior.

## Armas V1 — Etapa 7D.7A-1 (fundação canônica de 26 armas)

[DEFINITIVO — FUNDAÇÃO IMPLEMENTADA; EXECUÇÃO CONCLUÍDA NO BLOCO 2]

- O catálogo oficial passa a conter 26 armas: 8 Apoio, 7 Tática, 6 Estratégica e
  5 Especial. Radar (`radar`) é Tática; Central de Energia continua universal e fora
  do Random Cards, que passa a possuir 25 armas sorteáveis.
- Drone mantém o ID `drone`, deixa de ser reconhecimento e passa a ser instalação
  ofensiva fixa global: 6 níveis, Energy 30, cooldown 3 e dois disparos totais, no
  máximo um por instalação/round. Danos primeiro/segundo por nível: 1/1, 2/1, 2/2,
  3/2, 3/3 e 4/3. Instalação, captura e transporte preservam sequência, cargas e
  último round de disparo.
- Radar é instalação fixa global de inteligência, N1, Energy 25, cooldown 3 e uma
  revelação. A instalação criada na Resolution só poderá ser usada na Preparation
  seguinte. O resultado futuro será efêmero até o fim do round e não revelará nível,
  posição da Flak ou outras instalações.
- Lifecycle em VAZIO: Mina permanece; Bunker e Flak são destruídos; Central,
  Artilharia, Foguetes, Drone e Radar permanecem abandonados. Em NEUTRO, todas as
  instalações permanecem fisicamente; Mina conserva o instalador original e as
  demais ficam abandonadas/inoperantes.
- Captura preserva integralmente o estado restante e não cobra Energy, cooldown ou
  uso. Transporte Aéreo aceita Flak, Central, Artilharia, Foguetes, Drone ou Radar;
  Mina e Bunker não são transportáveis. Para destino aliado, somente a instalação
  selecionada muda de território e passa ao aliado, sem reinicialização.
- Mina temporariamente desarmada não dispara na ocupação e, ao rearmar, continua
  pertencendo ao instalador original.
- O bootstrap de desenvolvimento completa saves existentes com os 26 IDs sem apagar
  níveis, moedas, tokens, VIP ou decks. Saves antigos mantêm `drone`; Radar é apenas
  acrescentado quando aplicável.
- Os contratos legados de reconhecimento do Drone e infiltração territorial foram
  removidos atomicamente na etapa 7D.7A-2; não existe execução paralela.

## Armas V1 — Etapa 7D.7A-2 (registro histórico substituído)

[HISTÓRICO — as regras ativas posteriores prevalecem]

- Ordem global: início do round; Infiltrado; Paz; Anulador; transferências;
  interferências; reforços; instalações/logística; Sniper; Propaganda; ofensivas;
  Artilharia automática; Paraquedistas; terrestres; consolidação.
- Drone instala em território próprio e pode disparar no mesmo round por ação
  dependente. Cada instalação dispara no máximo uma vez por round; Flak intercepta,
  Bunker absorve e Paz impede antes do lançamento. A referência histórica a bloqueio
  por Sniper foi revogada pelas regras ativas posteriores.
  Alvos inimigos, Neutros e Vazios são válidos; Vazio consome carga com dano efetivo
  zero. O segundo disparo remove a instalação.
- Radar existente no início da Preparation usa uma porta imediata idempotente do
  `WDWeaponExecutionCoordinator`. Instalação do round corrente é inelegível. A leitura
  privada do autor detecta somente cobertura Flak e expira no próximo `BeginRound`.
  Flak, Paz e Sniper não bloqueiam a varredura.
- O contrato histórico por `ActionId` foi substituído por
  `TargetPlayerId + TargetWeaponId`; consultar a seção ativa de Infiltrado.
- `WDWeaponResolutionPipeline.ResolveRound` continua autoridade global única;
  `WDWeaponExecutionCoordinator` continua executor único e o GameManager apenas expõe
  portas finas de orquestração.
# Preparation, Resolution e UX funcional das Armas V1 [ATIVO]

## Autoridades canônicas

- `WDWeaponPreparationService` registra, substitui, cancela e congela intenções; não executa efeitos.
- `WDWeaponResolutionPipeline.ResolveRound` é a única autoridade global da Resolution.
- `WDWeaponExecutionCoordinator` é o executor canônico das armas.
- `GameManager` apenas orquestra as portas; `TerritorioClique` continua sendo o estado territorial real.
- A apresentação consome resultados imutáveis da Resolution e nunca recalcula nem reaplica gameplay.

## Preparation e HUD

- A Preparation dura 90 segundos e opera sobre o mapa real.
- O HUD apresenta oito armas em grade 2 × 4, Energy real, tropas disponíveis e cronômetro.
- Transferência aparece somente em partidas com aliados. A lista AÇÕES reúne reforços, ações terrestres, transferências, armas programadas e operações de instalações.
- Cartas distinguem disponível, selecionando, programada/cooldown prospectivo, cooldown real e indisponível.
- Cancelar uma instalação cancela deterministicamente suas operações dependentes ainda reversíveis. Um Radar que já revelou informação é irreversível.
- Blitzkrieg concede imediatamente seu limite terrestre ampliado durante a Preparation. Tropas Reservas e Recuperação concedem imediatamente um pool provisório distribuível, sem cobrar Energy/cooldown antes da Resolution.
- Ordens que excedam o limite normal e distribuições vinculadas ao benefício guardam dependência da ação de arma. Cancelar ou substituir a ação remove deterministicamente essas dependências e restaura limite, tropas disponíveis, territórios, Energy, cooldown e usos ao estado equivalente a nunca programá-la.
- A Resolution continua sendo a autoridade canônica: ela valida a ação e compromete seus custos, mas não duplica um benefício de planejamento que já foi materializado pela Preparation.
- Durante Resolution os comandos de Preparation ficam bloqueados; a lista, quando aberta, é somente leitura.
- O controle NORMAL / ACELERADO altera apenas apresentação e delays, nunca ordem, RNG ou resultado.

## Infiltrado [REGRA ATIVA]

- Energia: 20; cooldown próprio após uso válido: 4; cooldown forçado no `WeaponId` alvo: 3.
- Round 1: indisponível, contador 2; Round 2: indisponível, contador 1; Round 3: disponível.
- O alvo é `TargetPlayerId + TargetWeaponId`; a seleção por `TargetActionId` está obsoleta.
- A arma escolhida recebe cooldown forçado 3 mesmo que não tenha sido programada. Se estiver programada, sua ação é bloqueada pela ordem canônica.
- Infiltrado pode mirar outro Infiltrado. O primeiro válido por fase, OfficialPlayerOrder, ProgramOrder e ActionId prevalece.
- O Infiltrado executado recebe cooldown próprio 4; um Infiltrado atingido recebe cooldown forçado 3.
- Operações de instalações persistentes já existentes não são bloqueadas por essa seleção de arma.
- O bloqueio aplica-se ao uso/acionamento programado da carta naquele round; não
  desliga Radar, Drone, Foguetes, Artilharia, Flak, Central ou instalação persistente
  equivalente já existente.

## Radar [REGRA ATIVA]

- Radar tem uma revelação e pode usá-la na mesma Preparation em que a instalação é programada.
- A varredura é imediata, idempotente e revela somente se o território investigado possui cobertura de Flak.
- O resultado pertence exclusivamente ao autor, dura apenas o round atual e não expõe nível, posição da Flak nem outras instalações.
- Sniper não interfere com instalação, varredura, carga ou resultado do Radar.
- Flak, Paz e Infiltrado não bloqueiam a varredura. A Resolution apenas representa o evento já ocorrido, sem segunda execução e sem mecanismo especial de retry por Sniper.

## Sniper [REGRA ATIVA]

- Sniper é ofensivo, causa uma baixa quando executado validamente e somente então
  registra o bloqueio da origem atingida para ações terrestres e operações persistentes
  cuja interação já esteja definida.
- Paz vigente bloqueia Sniper antes do lançamento: não há dano, Energy, cooldown, uso
  nem bloqueio residual.
- Sniper não interfere com Radar nem Transporte Aéreo e não funciona como bloqueador
  universal de WeaponId ou instalações.

## Artilharia e Foguetes [REGRA ATIVA]

- Artilharia usa munição persistente, não duração operacional: N1/N2 = 6; N3/N4 = 9. Dano: 1/2/2/3.
- Dispara no máximo três vezes por round e no máximo uma vez por alvo, priorizando mais tropas e usando ID estável no desempate.
- Munição não utilizada permanece; Sniper impede a operação sem consumir estoque. Captura e transporte preservam munição e nível.
- Foguetes pode disparar no round da instalação, sem obrigatoriedade. Suas duas cargas são preservadas quando não usadas.

## Instalações, transporte e sigilo [REGRA ATIVA]

- Transporte Aéreo move tropas e, no máximo, uma instalação explicitamente transportável: Flak, Central, Artilharia, Foguetes, Drone ou Radar.
- A quantidade é escolhida na Preparation entre 1 e o menor valor entre capacidade por nível (5/7/10/12/15/18) e tropas da origem menos a guarnição obrigatória de 1.
- Ao chegar em território aliado, a instalação passa ao aliado e preserva integralmente nível, cargas, munição, duração e demais estados; isso não concede desbloqueio permanente.
- Bunker e Campo Minado não são transportáveis.
- Flak não intercepta Transporte Aéreo; Sniper também não impede tropas nem a instalação
  selecionada de serem transportadas.
- Campo Minado e Flak são visíveis somente ao proprietário e aliados; adversários e espectadores não os veem.
- Resultado de Radar é privado ao autor, inclusive em 2v2 e 3v3.

## Presentation funcional

- Resultados de armas são apresentados com carta compacta sobre o mapa e estados visuais distintos para sucesso, bloqueio, interceptação, invalidação e conflito/substituição.
- O popup de jogadores exibe avatar provisório, nome, patente, os quatro indicadores (territórios, tropas totais, tropas disponíveis e Energy) e os oito slots do deck em linha. Os perfis laterais seguem a `OfficialPlayerOrder` do round.
- A lista AÇÕES fecha ao entrar na Resolution, pode ser reaberta opcionalmente e passa a exibir os resultados canônicos sem controles de cancelamento ou operação.
- Instalações visíveis informam estado, cargas/munição, duração ou barreira sem duplicar a fonte autoritativa.
- Premium Polish, shaders, animações cinematográficas, chat completo, espectadores e easter eggs permanecem fora desta etapa.

## Labels de Regiões e autoria de mapas [ATIVO]

- A label compacta usa `<NOME> +<BÔNUS>` e nunca altera regra, membros ou bônus da região.
- Sem override, o runtime calcula o centroide dos centros territoriais e ancora a label no
  território membro mais próximo desse centroide. Isso evita que a média caia no oceano
  ou em espaço vazio distante da massa regional.
- Labels já posicionadas participam de uma resolução determinística de overlap: posição
  preferida, direita, esquerda, cima, baixo e diagonais, sempre limitada ao viewport.
- `DefinicaoRegiaoMapa` armazena opcionalmente `possuiDeslocamentoLabelManual` e
  `deslocamentoLabelManual`. Assets antigos usam automaticamente o fallback sem override.
- Na aba Regiões do War Dominion Map Editor, a label selecionada pode ser arrastada no
  preview. O comando **Resetar label para Automático** remove o override. Drag e reset
  suportam Undo/Redo, marcam o asset dirty e não movem territórios.
