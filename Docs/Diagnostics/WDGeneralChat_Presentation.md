# Chat Geral — calibração de apresentação

## 1. Background oficial

O Product Owner aprovou `Assets/Art/Backgrounds/Chat/Chat.png` e seu `.meta`.
A cena `Assets/Scenes/Home.unity` referencia a textura pelo GUID existente e
o controller a fornece ao Chat Geral. A arte não foi movida, duplicada ou editada.
O RawImage fica fora da Safe Area, preenchendo o viewport com UVs de cover centralizado.
Os demais 113 paths do antigo Grupo B continuam excluídos do escopo.
Esta aprovação não executa staging nem torna o asset versionado automaticamente.

## 2. Tipografia e emojis

Calibração exclusiva da view geral: mensagens/composer 20 px, placeholder 18 px,
previews/status 14 px, nomes 15 px, interlocutor 22 px e timestamps 12 px.
CHAT mantém 28 px e VOLTAR 16 px. Controles principais usam 14 px; títulos laterais 12 px.
O medidor do histórico usa o mesmo tamanho do corpo. Bubbles continuam com altura
calculada pelo texto; a composição, os painéis e as colunas não foram ampliados.
O sprite asset Noto Color compartilhado continua intacto. Sua proporção de 1,2 ×
fontSize produz aproximadamente 24 px nas mensagens e 16,8 px nos previews.
As duas barras longas do cabeçalho continuam ausentes.

## 3. ANEXOS — FUNCIONALIDADE APROVADA / IMPLEMENTAÇÃO FUTURA

O botão `+` do composer permanece visível no Chat Geral e no Chat da PARTIDA.
Ele é independente do Emoji Picker e permanece sem ação operacional.
A implementação futura deverá definir tipos permitidos, imagens, limites de tamanho,
armazenamento, segurança/validação, backend, upload, download/visualização e
comportamento mobile. Nenhuma dessas operações foi implementada neste pacote.

## 4. Validação

Compilação estática é distinta de validação Unity. Executar manualmente
`WDChatHubPanelTests`, regressões de Chat/Emoji e EditMode Run All, depois verificar
16:9/20:9, crop, legibilidade, emojis, drafts e envio. Não houve operação da Unity GUI.
