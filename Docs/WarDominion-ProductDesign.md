# War Dominion — Documento Mestre de Produto e Design

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

A coleção distingue arma não obtida, progresso parcial de Tokens, Tokens completos com Moedas insuficientes, upgrade disponível e nível máximo. Os dados atuais de catálogo são poucos mocks explicitamente identificados, apenas para validar fluxo, persistência e apresentação. O catálogo nominal possui 25 armas; efeitos, obtenção, custos e balanceamento ainda não revisados permanecem pendentes.

O estado local atual utiliza um repositório substituível por conta/backend. Nomes e composições dos cinco decks, inventário, níveis, Tokens, Moedas, Esmeraldas e estado VIP são persistidos sem acoplar a interface ao armazenamento. O visual desta seção é **[PROVISÓRIO]** até o Premium Polish.

A modalidade futura **Deckbuilder** é completamente distinta dos slots de decks salvos: ela construirá dinamicamente um deck antes do Round 1 e não foi implementada nesta fase.

### [DEFINITIVO] Catálogo nominal de 25 armas

1. Ataque Aéreo
2. Campo Minado
3. Sabotagem
4. Reforços
5. Blitzkrieg
6. Drone
7. Bomba Atômica
8. Anulador de Transferência
9. Sniper
10. Produção Bélica
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

**Anulador** não é uma arma adicional: o nome correto é **Anulador de Transferência**. **Mercenários** é o nome oficial que substitui “Tropas Mercenárias”.

### [DEFINITIVO] Regra global de dano e conquista

Armas que causam dano podem reduzir um território até zero tropas. Quando o dano for maior ou igual à guarnição, o território passa ao estado **VAZIO**, perde o proprietário, permanece com zero tropas e usa a aparência neutra/bege definida para esse estado.

Armas de dano não conquistam território. A conquista ocorre somente por movimento terrestre válido ou por **Paraquedistas**, quando sua mecânica própria de conquista for satisfeita.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Ataque Aéreo

- Energia: 20; uso: 1; cooldown geral previsto: três rounds.
- Alvo: território adversário vizinho.
- Dano por nível: N1 2, N2 3, N3 4, N4 5, N5 6, N6 7.
- Ocorre antes dos movimentos e ataques terrestres do jogador.
- Flak pode interceptar; Paz pode impedir e qualquer interação territorial de Infiltrado será definida na matriz completa.
- Bunker de qualquer nível protege completamente aquele território contra Ataque Aéreo, independentemente da quantidade de tropas.
- Não conquista território.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Campo Minado

- Energia: 15; uso: 1.
- É instalado secretamente em território próprio e permanece oculto de adversários e espectadores.
- Permanece instalado por quantos rounds forem necessários até ser acionado; não expira simplesmente ao final do round.
- É acionado pelo primeiro ataque terrestre ou uso aplicável de Paraquedistas naquele território.
- Dano por nível: N1 2, N2 3, N3 4, N4 5, N5 6, N6 7.
- Causa até o número de baixas possível; dano excedente é perdido e, após o acionamento, a mina é consumida.
- Se Paz impedir o ataque antes de ele ocorrer, a mina não é consumida. Uma eventual interação territorial de Infiltrado será definida na matriz completa.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Sabotagem

- Energia: 20; uso: 1; alvo: um jogador adversário individual.
- Afeta somente as tropas naturais de reforço daquele round: reforço derivado dos territórios e bônus de regiões.
- Não afeta Reforços, Tropas Reservas, Mercenários nem tropas previamente existentes.
- Percentuais: N1 15%, N2 20%, N3 25%, N4 30%, N5 35%, N6 40%.
- A quantidade sabotada é arredondada para baixo.
- Infiltrado não deve ser presumido como bloqueio global de Sabotagem; a matriz completa consolidará essa ausência de interação.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Reforços

- Energia: 25; uso: 1; cooldown geral previsto: três rounds.
- Escolhe um único território próprio ou, em modo de equipe, um território aliado.
- As tropas são adicionadas diretamente ao território escolhido.
- Quantidade por nível: N1 3, N2 4, N3 5, N4 6, N5 7, N6 8.
- Sabotagem não afeta esta arma. Infiltrado só poderá interferir quando o reforço for destinado especificamente ao território infiltrado, conforme a matriz completa.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Blitzkrieg

- Energia atual: 15; uso: 1.
- O custo permanece provisório até a revisão comparativa global de Energia.
- Concede ataques terrestres adicionais além do limite padrão de três: N1 +1, N2 +2, N3 +3 e N4 +4.
- Os máximos correspondentes no round são 4, 5, 6 e 7 ataques terrestres.
- Possui somente quatro níveis.
- Paz poderá interferir; qualquer efeito de Infiltrado depende do território de origem envolvido e será definido na matriz completa.

### [DEFINITIVO — MECÂNICA PRINCIPAL, NÃO IMPLEMENTADO] Drone

Drone é uma arma própria do War Dominion.

- Energia: 25; pode atuar em qualquer lugar do mapa.
- A ativação já consome Energia e inicia cooldown, mesmo que o jogador desista posteriormente do disparo.
- Possui reconhecimento de Torre Antiaérea/Flak Tower; o reconhecimento não pode ser usado gratuitamente para mapear defesas.
- Progressão: N1 1 reconhecimento e até 2 tropas de dano; N2 1 e até 3; N3 1 e até 4; N4 2 e até 3; N5 2 e até 4; N6 2 e até 5.
- Após consumir os reconhecimentos permitidos, não pode testar outros territórios naquela ativação.
- O disparo deve escolher um dos territórios reconhecidos. O jogador pode desistir, mas Energia e cooldown não retornam.
- Flak ainda pode interceptar conforme sua chance; Paz pode impedir e qualquer interação territorial de Infiltrado será definida na matriz completa.
- Não conquista território.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Produção Bélica

- Energia: 10; quatro níveis; cooldown especial de cinco rounds; máximo de duas ativações/instalações por jogador em cada partida.
- Produção por round durante três rounds: N1 +5, N2 +10, N3 +15 e N4 +20 de Energia.
- É uma instalação territorial. Sua força econômica elevada é intencional e não deve ser reduzida automaticamente por parecer uma escolha muito forte.
- Se o território for conquistado, a instalação é capturada e o novo proprietário recebe as parcelas restantes. A duração não reinicia; o conquistador não paga Energia, não consome ativação da própria arma e não altera seu cooldown.
- A instalação em funcionamento pertence ao território e ao proprietário atual, enquanto a ativação original continua atribuída a quem a utilizou.

### [DEFINITIVO — DIREÇÃO, DETALHES PENDENTES] Bomba Atômica

A Bomba Atômica mantém deliberadamente grande potencial de dano. Seu efeito não deve ser reduzido automaticamente apenas por atingir vários territórios vizinhos: custo elevado de Energia e possibilidade de preparação e defesa estratégica integram o balanceamento. Os detalhes completos ainda serão consolidados na matriz correspondente.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Transporte Aéreo

- Energia: 5; cooldown geral previsto: três rounds.
- Capacidade: N1 5, N2 7, N3 10, N4 12, N5 15 e N6 18 tropas.
- Transporta tropas de território próprio para outro território próprio ou aliado, em qualquer lugar do mapa.
- Deve permanecer ao menos uma tropa na origem quando aplicável às regras normais de propriedade.
- Pode transportar junto uma instalação fixa compatível existente na origem, como Artilharia, Foguetes, Produção Bélica, Bunker ou Flak Tower. Campo Minado não é transportável.
- A instalação mantém integralmente duração, disparos, barreira, produção e demais estados restantes.
- Se o destino já possuir instalação do mesmo tipo, a ação é bloqueada com informação apropriada; tropas e instalação não se movem nem são perdidas. Instalações diferentes podem coexistir no território, salvo futura exceção explícita.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Kamikaze

- Energia: 25; cooldown geral previsto: três rounds.
- Dano: N1 2, N2 3, N3 4, N4 5, N5 6 e N6 7.
- A origem precisa possuir ao menos duas tropas. Uma delas é usada como Kamikaze e sacrificada.
- Alcance de até três territórios/conexões; não conquista território.
- Flak pode interceptar. Se isso ocorrer, a tropa Kamikaze continua perdida e o alvo não sofre dano.
- Bunker protege completamente contra o dano Kamikaze. Paz e uma eventual interferência territorial de Infiltrado serão consolidadas na matriz.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Bomba de Longo Alcance

- Energia: 25; cooldown geral previsto: três rounds.
- Pode atingir qualquer território inimigo do mapa.
- Dano: N1 2, N2 3, N3 4, N4 5, N5 6 e N6 7.
- Flak pode interceptar; Bunker protege completamente; não conquista território.
- Paz e uma eventual interferência territorial de Infiltrado serão consolidadas na matriz.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Paz

- Energia: 15; nível único.
- Durante a Preparação, jogadores podem programar Paz entre si porque as escolhas ainda não foram resolvidas.
- Na Resolução, é processada conforme a ordem de jogadores daquele round. Uma Paz válida estabelece a relação entre os dois jogadores e impede ações ofensivas posteriores entre eles conforme a matriz final; ambos continuam livres contra outros jogadores.
- Em Paz recíproca, a primeira ação válida estabelece a relação. Quando a segunda chegar à resolução, será apresentada como anulada; seu jogador não perde Energia nem entra em cooldown.
- Regra econômica geral: programar uma arma na Preparação não significa consumir Energia imediatamente. Em regra, o consumo ocorre quando a ação consegue ser executada. Exceções precisam ser explícitas, como o Drone, cujo reconhecimento já consome Energia e inicia cooldown.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Artilharia

- Energia: 30; instalação fixa territorial.
- N1: 1 dano por alvo, até 3 alvos, por 2 rounds.
- N2: 2 danos por alvo, até 3 alvos, por 2 rounds.
- N3: 2 danos por alvo, até 3 alvos, por 3 rounds.
- N4: 3 danos por alvo, até 3 alvos, por 3 rounds.
- É automática: a cada round operacional, o servidor identifica os territórios inimigos diretamente vizinhos, ordena-os da maior para a menor quantidade atual de tropas e seleciona até três. Empates usam critério determinístico comum; sem alvo válido, não há disparo naquele round.
- O jogador não escolhe alvos a cada round. A seleção é recalculada em cada operação e pode repetir ou trocar completamente os alvos.
- Resolve antes dos combates terrestres e pode ser destruída por armas compatíveis, conforme a matriz futura.
- Se o território for conquistado, é capturada e continua para o novo proprietário pelo tempo restante. A duração não reinicia e a captura não consome arma, Energia, ativação ou cooldown do conquistador.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Foguetes

- Energia: 25; cooldown geral previsto: três rounds; instalação fixa territorial.
- Cada instalação possui exatamente dois disparos. Dano por disparo: N1 2, N2 3, N3 4, N4 5, N5 6 e N6 7.
- Não expira por rounds: permanece até consumir os dois disparos, ser destruída ou capturada.
- Alcance de até três territórios/conexões a partir da instalação.
- Não é automática. O jogador programa quando disparar e escolhe um alvo válido; pode instalar e guardar os disparos.
- No máximo uma nova instalação no round permitido pela arma. Podem coexistir instalações do mesmo jogador em territórios diferentes, mas nunca duas de Foguetes no mesmo território. Uma instalação antiga ainda ativa não impede outra quando a arma voltar a estar disponível.
- A contagem é 2 → 1 → 0, quando a instalação é encerrada/removida.
- Se capturada, o novo proprietário recebe exatamente os disparos restantes, sem pagar Energia nem consumir sua própria ativação/cooldown.
- Resolve antes dos combates terrestres. Interações com Flak, Bunker e destruição serão fechadas na matriz.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Torre Antiaérea / Flak Tower

- Energia: 0; duração: três rounds; instalação secreta com quatro níveis.
- Chance de interceptação: N1 50%, N2 65%, N3 80% e N4 90%.
- Protege o território onde está instalada e territórios próprios ou aliados diretamente vizinhos.
- A matriz final poderá incluir Ataque Aéreo, Drone, Bomba Atômica, Bomba de Longo Alcance, Bomba Química, Kamikaze e Paraquedistas entre as ações interceptáveis.
- Um território não pode conter duas Flaks. Se áreas de Flaks distintas se sobrepuserem, não somar probabilidades nem fazer múltiplas tentativas: usar somente a Flak de maior nível que cobre o alvo.
- Se a Flak que cobre o território central interceptar uma Bomba Atômica, a bomba é destruída antes da detonação e não produz dano central nem colateral.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Bunker

- Energia: 5; quatro níveis; barreira separada das tropas: N1 2, N2 3, N3 4 e N4 5.
- Ataques que precisam atravessá-lo reduzem primeiro a barreira. Apenas o excesso aplicável depois de destruí-la alcança as tropas; o feedback futuro deve representar separadamente desgaste da barreira e guarnição.
- Não pode haver dois Bunkers empilhados no mesmo território.
- Protege totalmente contra Ataque Aéreo, Bomba de Longo Alcance, Kamikaze e Bomba Química.
- Contra Bomba Atômica, protege o próprio território, mas não impede a detonação nem o dano colateral aos vizinhos.
- Paraquedistas precisam atravessar ou destruir a barreira antes de enfrentar as tropas.
- Se capturado sem ser destruído, passa ao novo proprietário com a barreira restante e sem regeneração automática.
- Sniper ignora a barreira. Se eliminar a única tropa, o território fica Vazio e o Bunker é removido/destruído junto com a perda do domínio.

### [PENDENTE] Campo Minado em território Vazio

Se um território possuir uma tropa e Campo Minado, e o Sniper eliminar essa tropa, o território passa a Vazio e a mina permanece enterrada. Ainda será definido quem poderá acionar posteriormente uma mina em território sem proprietário; não inferir esse comportamento antes da matriz global.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Propaganda de Guerra

- Energia: 25.
- Limite por nível: N1 até 3, N2 até 4, N3 até 5, N4 até 6, N5 até 7 e N6 até 8 tropas. Uma única tropa acima do limite faz a arma falhar completamente.
- Em caso de sucesso, o território perde o proprietário, torna-se Neutro e mantém metade das tropas anteriores, com divisão inteira para baixo: 8 → 4, 7 → 3, 5 → 2 e 3 → 1.
- Ignora Bunker, Campo Minado, Flak e Paz. A interação com Infiltrado territorial permanece para a matriz.
- Sniper obrigatoriamente resolve antes de Propaganda. Assim, um território com 9 tropas pode cair para 8 por Sniper e então se tornar elegível para Propaganda N6.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Bomba Química

- Energia: 25; alvo: território inimigo diretamente vizinho.
- Dano percentual: N1 25%, N2 30%, N3 35%, N4 40%, N5 45% e N6 50%.
- Arredondar baixas para cima, com mínimo de uma baixa quando a porcentagem positiva aplicada a um alvo válido produzir valor inferior a um. Exemplo: 7 × 50% = 3,5, portanto 4 baixas e 3 sobreviventes.
- Bunker protege completamente; Flak pode interceptar; Paz pode impedir; uma eventual interação territorial de Infiltrado será definida na matriz.
- Não conquista território.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Paraquedistas

- Energia: 25; quatro níveis: N1 2, N2 3, N3 4 e N4 5 paraquedistas.
- Pode atuar em qualquer lugar do mapa para atacar território inimigo ou reforçar território próprio/aliado.
- Paraquedistas são tropas reais e constituem a exceção entre as armas: podem conquistar território.
- No ataque, seguem lógica semelhante ao combate terrestre. Precisam atravessar/destruir Bunker; podem acionar Campo Minado; sobreviventes de uma conquista permanecem como tropas do conquistador.
- Se Flak interceptar, nenhum paraquedista chega e a operação é perdida.
- Quando usados como reforço, chegam antes de ataques terrestres programados dependentes daquele território. O combate usa as tropas efetivas após Sabotagem do reforço natural e a chegada dos Paraquedistas.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Tropas Reservas

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

A coluna 6+ representa seis ou mais territórios perdidos. As tropas entram no pool de reforços do jogador. Sabotagem e Infiltrado não afetam Tropas Reservas. O servidor deriva as perdas efetivas do round anterior e não confia em informação fornecida pelo cliente.

### [DEFINITIVO — DIREÇÃO, MATRIZ PENDENTE] Infiltrado

- Energia: 25; nível único; cooldown geral previsto: três rounds.
- Atua sobre um território/local específico e não bloqueia genericamente todas as ações de um jogador.
- Pode interferir em ações ligadas ao território infiltrado, como ataque ou movimento originado dali; Artilharia, Foguetes ou Produção Bélica instalados ali; Transporte Aéreo originado dali; e Reforços, Paraquedistas ou parcela de Mercenários destinados ao local.
- Não presumir interferência em ações globais sem localização territorial, como Paz, Sabotagem e Anulador de Transferência.
- A matriz Infiltrado × 25 armas ainda será criada e validará cada interação territorial.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Recuperação de Tropas

- Energia: 15; recuperação máxima: N1 4, N2 5, N3 6, N4 7, N5 8 e N6 9.
- Escolhe um território que pertencia ao jogador, foi atacado no round anterior, perdeu tropas e continuou sob domínio do mesmo jogador.
- Recupera diretamente nesse território e não entra no pool geral.
- Nunca ultrapassa as baixas efetivamente registradas ali. Exemplo: perdeu 3 com N6, recupera 3; perdeu 12 com N6, recupera 9.
- O servidor registra as baixas reais por território.

### [DEFINITIVO — GAME DESIGN FECHADO, NÃO IMPLEMENTADO] Mercenários

- Energia: 20; cooldown geral previsto: três rounds.
- Escolhe na ativação o próprio jogador ou um aliado; o mesmo beneficiário recebe todas as parcelas durante três rounds.
- Progressão: N1 1/1/1, N2 2/1/1, N3 2/2/1, N4 2/2/2, N5 3/2/2 e N6 3/3/2.
- As parcelas entram no pool de reforços do beneficiário e ocorrem automaticamente nos rounds seguintes após uma ativação válida.
- Sabotagem não reduz Mercenários. Internamente, preservar a origem: reforço natural/regiões é sabotável; Mercenários não é.
- Infiltrado não cancela Mercenários globalmente; só poderá interferir em uma parcela destinada ao território específico infiltrado, conforme a matriz final.

### [DEFINITIVO] Categorias funcionais das armas

Categoria organiza identidade e comportamento; não representa raridade nem necessariamente poder.

- **Apoio — 8:** Ataque Aéreo, Reforços, Transporte Aéreo, Flak Tower, Bunker, Tropas Reservas, Recuperação de Tropas e Mercenários.
- **Tática — 6:** Campo Minado, Blitzkrieg, Anulador de Transferência, Sniper, Kamikaze e Paraquedistas.
- **Estratégica — 6:** Sabotagem, Produção Bélica, Bomba de Longo Alcance, Paz, Artilharia e Foguetes.
- **Especial — 5:** Drone, Bomba Atômica, Propaganda de Guerra, Bomba Química e Infiltrado.

Total: 25 armas, distribuídas em 8/6/6/5.

### [PENDENTE / V1 ESTRUTURAL] Ordem global de resolução das armas

Esta ordem não é especificação final de implementação. Será revisada após a matriz completa de interações. Princípios já confirmados:

- Paz resolve cedo, antes das ações ofensivas que poderá impedir.
- Infiltrado é territorial e estabelece a condição antes das ações ligadas ao local.
- Sabotagem afeta apenas reforço natural baseado em territórios e bônus de regiões.
- Mercenários, Tropas Reservas, Reforços, Recuperação de Tropas e Paraquedistas não são automaticamente reforço natural sabotável.
- Sniper resolve obrigatoriamente antes de Propaganda de Guerra.
- Paraquedistas usados como reforço resolvem antes de ataques terrestres dependentes daquelas tropas.
- Artilharia e disparos de Foguetes programados para o round resolvem antes dos combates terrestres.
- Artilharia é automática e seus alvos são escolhidos pelo servidor; Foguetes são programados pelo jogador durante a Preparação e executados pelo servidor na Resolução.
- Alterações produzidas por armas anteriores integram o estado usado pelo combate terrestre.
- Ao final, o servidor consolida tropas, proprietários, estados Neutro/Vazio, instalações, barreiras, minas, disparos, durações, Energia, cooldowns, territórios perdidos, baixas por território, parcelas futuras, vitória e dados do próximo round.

A direção macro continua sendo preparação secreta, diplomacia, interferência, economia/reforços, instalações/defesas, ações especiais, ataques, movimentos e consolidação final, sujeita à revisão completa.

### [PENDENTE] Matriz e balanceamento antes da implementação

O sistema completo de armas não deve ser implementado ainda. Próximas etapas:

1. criar a matriz das 25 armas, cobrindo alvo, origem territorial/global, Flak, Bunker, Paz, Infiltrado, Campo Minado, captura, destruição, ordem, consumo de Energia, cooldown e resultados de falha/anulação/interceptação;
2. resolver todos os conflitos encontrados;
3. consolidar a ordem global definitiva;
4. revisar comparativamente Energia, cooldowns, níveis, duração, usos e força relativa;
5. definir separadamente custos padrão de progressão em Tokens específicos e Moedas;
6. somente então produzir a especificação de implementação.

### [STATUS EM 07/09/2026] Revisão das armas

- Catálogo nominal: 25 armas.
- A revisão conceitual inicial das 25 armas foi concluída.
- Nenhuma implementação runtime deve ser inferida destas decisões documentais.
- Custos de Energia ainda passarão por revisão comparativa global.
- Ordem global permanece V1 estrutural e pendente.
- A matriz completa de interações ainda não foi criada.
- Tokens e Moedas necessários à progressão serão definidos separadamente pelo proprietário, com valores padrão.

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

No modelo 2v2 atual há apenas um aliado elegível, portanto não é necessário perguntar o destinatário: a seleção direta do território é suficiente. A arquitetura deve continuar extensível para outros formatos.

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
