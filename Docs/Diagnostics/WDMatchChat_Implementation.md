# Match UI V2 — Chat da partida

Referência visual: [Figma 119:338](https://www.figma.com/design/lHqfLKfZxCsv9xmpCUy5Jl/War-Dominion?node-id=119-338).
Implementação: 25/09/2026. Validação visual em Unity pendente; nenhuma GUI/Test Runner foi operada.

## A–B. Auditoria e reutilização

- `WDChatRepository` é a autoridade local de sessão compartilhada pela Home e partida: canais privados, clã, equipe temporária, histórico, envio local, recebimento via `ReceiveLocalMock`, unread booleano e mute.
- `WDProfileRepository` fornece identidade; `WDSocialRepository` fornece amigos, permissões e presença. Seus dados atuais são de protótipo/mocks já existentes; esta implementação não adiciona seeds.
- A apresentação antiga é `WDChatHubPanel`, usada pela Home e anteriormente pelo HUD legado. Ela não é a autoridade visual desta implementação e não foi modificada.
- `UICompositionRoot` já criava o canal temporário, mas descartava o retorno. Agora passa a referência existente ao binding.
- O canal PARTIDA existente é de equipe: somente equipes de dois ou três aliados. FFA/1×1 sem canal ficam indisponíveis; nenhuma regra de gameplay foi criada.

## C–E. Integração e fechamento

`UICompositionRoot → WDMatchUIPresentationBinding → IWDMatchChatPresentation → WDMatchUIV2 → WDMatchChatView`.

O botão Chat mantém seu único callback existente para `WDMatchUtilityCommands.Toggle(Chat)`.
`PresentUtilitySelection` controla a visibilidade. Menu/Regiões/Chat continuam exclusivos pela autoridade existente, cujo código não foi alterado.

O backdrop usa `Button` e chama `CloseActive` somente quando Chat é a utility ativa. O painel possui `EventTrigger` que consome o clique interno. O sistema UI existente atende mouse/touch. O backdrop cobre todo o Canvas e bloqueia o mapa, inclusive fora da Safe Area. Um filtro permite somente os toggles existentes Chat/Menu/Regiões, fora do painel; não cria novos controles.

## F–G. Dados e limitações

- Conversas/recentes, mensagens, remetente e timestamps vêm do repositório compartilhado.
- Envio é local, através de `Send`; não há confirmação de entrega por rede. Não se apresenta “Entregue”.
- O painel aberto limpa unread apenas do canal acessível em exibição; fechado não consome unread. Como a autoridade é booleana, usa ponto, sem inventar contagem.
- Atualizações do histórico não recriam o composer nem apagam rascunhos. Troca de conversa preserva rascunhos por canal durante a vida da apresentação.
- Amigos online e contador usam `Friends`/`PresenceOf`; VER TODOS alterna a lista existente incluindo offline. Clique em amigo e busca por nickname/ID exato usam `OpenPrivate` e suas permissões reais.
- CLÃ usa `ClanChannel`; opções permitem mute do canal existente. Sem atividade musical inventada.
- Suporte, anexo do composer e Discord sem autoridade/configuração estão desabilitados. Nenhuma URL foi inventada.

## H. Assets oficiais

Diretório: `Assets/Resources/UI/Match/Chat/`.
SVGs originais preservados e rasterizados a 4× para uGUI, sem redesenho; PNGs com alpha. Discord é a imagem original opaca, recortada em cover e mascarada em runtime.

| Node Figma | Asset runtime (PNG; SVG correspondente preservado) |
|---|---|
| 119:119 | Globe |
| 119:129 | Clan |
| 119:253 | Plus |
| 119:146 | HexCyan |
| 119:160 | HexNeutral |
| 119:184 | Support |
| 119:210 | Options |
| 119:260 | Send |
| 119:273 | Friends |
| 119:317 | ArrowRight |
| 119:335 | External |
| 119:320 | Discord — imagem original, sem SVG |

Inter Regular/SemiBold/Bold em `Assets/Resources/UI/Fonts/` são cópias idênticas das fontes já existentes em `Assets/TextMesh Pro/Fonts/`, com GUIDs próprios, sem alteração de tipografia global.

## I–J. Visual e responsividade

- Painel 1400×760; centro com deslocamento vertical −18 na referência 1920×1080; raio 20; borda #C59B63.
- Colunas canônicas 280/770/350, descontando bordas de 1 unidade; altura interna aproximada 724.
- Backdrop preto 55%; superfície #0B0E12 a 90%; colunas #0E171D a 75%; centro #0A0F13 a 55%.
- Cyan #20C7E8; secundário #AAB9C2; presença #39D98A; separadores #31515D.
- Inter: cabeçalho 18, mensagens/composer 14, nomes/controles 12, metadados 10, badge 9. Sem autosizing e sem rich text para conteúdo de jogadores.
- Bubbles 480 de largura, altura calculada pelo texto; recebidas à esquerda, enviadas à direita; ScrollRect vertical com alinhamento inferior quando o histórico é curto.
- Composer de 56; send com alvo 44×44 e glyph 20. Principais ações locais usam alvos de 44 ou mais na escala de referência, sem ampliar glyphs.
- O painel reduz uniformemente se necessário para caber na Safe Area; não altera Canvas/framing/mapa/LayoutPartida nem cria letterbox. Mantém as três colunas em 16:9/20:9. Legibilidade física e teclado touch requerem inspeção no dispositivo.
- Primitivas uGUI arredondadas reutilizam a fábrica do projeto. Sombras difusas do navegador/Figma não foram reproduzidas como filtros CSS; acabamento final depende da inspeção em Unity.

## K. Arquivos de código

Criados:

- `Assets/Scripts/UI/WDMatchChatView.cs` e `.meta`.
- `Assets/Scripts/UI/WDMatchChatBackdropFilter.cs` e `.meta`.
- `Assets/Editor/Tests/WDMatchChatViewTests.cs` e `.meta`.

Alterados somente para integração do Chat:

- `Assets/Scripts/UI/UICompositionRoot.cs`.
- `Assets/Scripts/UI/WDMatchUIPresentationBinding.cs`.
- `Assets/Scripts/UI/WDMatchUIV2.cs`.

Além disso: assets/fontes listados acima, suas metas e este relatório. Os projetos gerados receberam includes temporários para compilação dos arquivos novos e foram restaurados em seguida.

## L–M. Verificações

- 15 casos EditMode adicionados: callback/toggle, exclusividade bidirecional Menu/Regiões, clique interno/externo mouse e touch, filtro do backdrop, envio/dados/timestamps, draft e unread ao fechar/reabrir, permissões/encerramento, amigos/privados/recentes, estados vazios, crescimento/segurança de texto e bounds de controles em três tamanhos de Safe Area.
- Testes existentes de Menu/Regiões permanecem inalterados. Test Runner não executado.
- Builds sequenciais `Assembly-CSharp.csproj` e `Assembly-CSharp-Editor.csproj`: 0 erros, 0 avisos.
- 11 PNGs de ícones verificados com alpha mínimo 0/máximo 255. Imagem Discord é opaca por origem e tem clipping próprio em UI.
- 30 GUIDs novos verificados como únicos em Assets. Três fontes verificadas byte a byte contra a origem.
- Whitespace dos novos códigos e diff da integração verificados. O diff global possui whitespace pré-existente em `ProjectSettings/ProjectSettings.asset`, que não foi alterado por esta tarefa.
- Comparação com hashes iniciais dos scripts: somente os três arquivos de integração acima mudaram; código dos blocos congelados e autoridades funcionais foi preservado.
- Logs locais: `.utmp/chat-runtime-build.log`, `.utmp/chat-editor-build.log`, `.utmp/chat-assets-verification.json`; baseline em `.utmp/chat-before/`.

## N–O. Inspeção manual necessária

Dados do mockup Figma não são conteúdo de produção; lista/online/histórico podem diferir conforme os repositórios locais. Contagem numérica unread, música, entrega por rede, anexos, suporte e comunidade não são simulados.

1. Aguardar importação dos assets e conferir Console; abrir partida e alternar Chat duas vezes.
2. Alternar Chat/Menu/Regiões; testar clique/toque no mapa escurecido versus dentro do painel.
3. Em equipe de dois/três aliados, enviar mensagem; conferir conteúdo/horário e rolagem. Em FFA/1×1, conferir estado indisponível da PARTIDA e privados/clã disponíveis conforme dados.
4. Abrir privado por amigo/RECENTES/+; verificar permissões, mute, rascunho ao fechar/reabrir e listas vazias.
5. Comparar o visual com 119:338 em 1920×1080 e 20:9 com Safe Area; conferir teclado touch, campo e envio em dispositivo.
6. Executar manualmente `WDMatchChatViewTests`, `WDChatTests` e os testes existentes de `WDMatchUIV2Tests`/Regiões.

Nenhum commit/push/reset/rebase/clean, edição de Figma ou operação de Unity GUI/Test Runner foi realizado. Configurações não foi implementada.
