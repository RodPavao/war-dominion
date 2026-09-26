# Chat — Emoji Picker reutilizável

Data: 2026-09-26. Escopo: Unicode no composer da partida; sem Chat geral ou reações de gameplay.

## A. Arquitetura

`WDChatEmojiPicker` recebe TMP_InputField, Button e RectTransform delimitador. Não acessa repositories, canais, participantes ou utilities. `WDChatEmojiText` fornece fallback local e inserção atômica; `WDChatEmojiToggleFilter` permite tocar novamente no botão através da camada local.

## B. Integração

`WDMatchChatView` cria o botão 44×44 em x=60/y=6 no campo existente. O input passa de x=64/largura=600 para x=112/largura=552: reserva adicional de 48 px. Altura, botão +, enviar, painel e colunas permanecem iguais. O host fecha o picker antes de fechar o Chat ou selecionar outra conversa. Nenhuma autoridade de seleção/envio foi criada.

## C. Catálogo

34 escolhas: Carinhas (15), Gestos (9), Símbolos (10), além de Recentes. Inclui pares surrogate e sequências com variation selector (✌️ e ❤️). Grade de seis colunas; ScrollRect vertical com RectMask2D.

## D. Recentes

Lista compartilhada durante a sessão, máximo 16, escolha aceita movida ao início e sem duplicatas. Estado vazio explícito. Não há persistência entre sessões, backend ou alteração das preferências existentes.

## E. Unicode e fontes

Auditoria: com.unity.ugui 2.0.0 incorpora TMP; Chat usa Inter, sem fallback emoji configurado no TMP Settings. Assets legados de emoji usam texto SVG com Segoe UI Emoji e não servem como ícone portátil. Não foi alterada a configuração global de fontes.

Foi incorporada Noto Emoji monocromática como TTF estática, peso 400, derivada da fonte variável oficial. Fonte incluída em Resources; TMP cria atlas SDF dinâmico 2048², com múltiplos atlas. Uma cópia em memória da fonte do Chat recebe o fallback; a fonte original permanece intacta. Composer, picker e corpos de mensagens recebidas/enviadas usam esse caminho. Rich Text permanece desligado. Seletores FE0E/FE0F e joiner 200D têm glyph invisível sem mudar a string.

Fonte: https://github.com/google/fonts/tree/main/ofl/notoemoji

Licença SIL OFL 1.1 incluída em `Assets/Resources/UI/ChatEmoji/OFL.txt`.

TTF: 886628 bytes; SHA256 `785124d1cfa4c62e13db7b50b4574413d906e0a977d58b87177afbab549ba1f0`.

Referências: [fallbacks TMP](https://docs.unity.cn/Packages/com.unity.textmeshpro%404.0/manual/FontAssetsFallback.html), [formatos Noto](https://notofonts.github.io/noto-docs/website/use/). A análise do comportamento efetivo de TMP foi feita no pacote local instalado.

## F. Multiplataforma

Fonte incorporada, sem dependência de Segoe/Apple/Android emoji ou suporte COLR. Aparência monocromática intencional, tingida como texto. Cobertura cmap confirmada para todos os caracteres base do catálogo. Windows, Android e iOS ainda exigem validação no player: compilação C# não prova renderização/teclado de dispositivo. Sequências complexas externas ao catálogo (famílias ZWJ, modificadores, bandeiras) não têm garantia de composição; podem aparecer como caracteres separados. Unicode arbitrário fora da cobertura continua sujeito a glyph ausente. O texto original nunca é convertido para códigos proprietários.

## G. Inserção

Preserva cursor/seleção UTF-16 capturados antes da perda de foco. Substitui seleção em ambos os sentidos, ajustando limites de elementos Unicode para não cortar surrogate pairs. Atualiza cursor após a escolha e não chama envio. Respeita characterLimit sem truncar emoji e consulta onValidateInput. Campos com validadores especializados são recusados conservadoramente, sem contorná-los. O composer atual não tem esses validadores; WDChatRepository não define limite máximo adicional. O envio continua exclusivamente no fluxo existente.

## H. Fechamento

Toggle; clique na camada transparente dentro do Chat; fechamento do Chat; seleção de conversa. EventTrigger no corpo do popup impede propagação do clique para fechar o Chat. Camada restrita ao painel, sem modificar backdrop global. Escolha fecha picker e devolve foco em runtime; restauração pendente é cancelada quando o host fecha/troca conversa.

## I. Touch

Botão e categorias com altura 44; células 48×48, em unidades do Canvas existente. Popup 336×260, padding 8, raio 12, fundo SurfaceMatchReadable (95%), borda dourada, seleção cyan. Posição calculada acima do botão e limitada ao painel do Chat, que já é ajustado à Safe Area. Sem novo Canvas, dependência de hover ou abertura de teclado ao selecionar categoria. Host reutilizado deve comportar o popup. Nenhuma regra específica por aparelho.

## J/K. Arquivos e assets

Alterados:
- `Assets/Scripts/UI/WDMatchChatView.cs`
- `Assets/Editor/Tests/WDMatchChatViewTests.cs`

Criados, com metas:
- `Assets/Scripts/UI/WDChatEmojiPicker.cs`
- `Assets/Scripts/UI/WDChatEmojiText.cs`
- `Assets/Scripts/UI/WDChatEmojiToggleFilter.cs`
- `Assets/Editor/Tests/WDChatEmojiPickerTests.cs`
- `Assets/Resources/UI/ChatEmoji/Smiley.svg` e `Smiley.png` (glyph gráfico próprio, estável; PNG 96×96)
- `Assets/Resources/UI/ChatEmoji/NotoEmoji-Regular.ttf` e `OFL.txt`
- meta da pasta ChatEmoji.

Este relatório foi criado em Docs/Diagnostics. Logs e snapshots temporários estão em `.utmp`. Não há nova dependência no projeto. fontTools foi usado somente como ferramenta local para gerar a instância estática. Os csproj receberam inclusões temporárias para compilar scripts novos antes da importação pelo Unity e foram restaurados byte a byte ao estado anterior.

## L. Testes

`WDChatEmojiPickerTests`: 9 casos cobrindo toggle/categorias/vazio, cursor, seleção normal/invertida, surrogate, limite/validação, Recentes, bounds/touch/scroll e cobertura do fallback. Fixture sem Match ou repository.

`WDMatchChatViewTests.EmojiPickerStaysLocalClosesWithChatAndHistoryKeepsLiteralUnicode`: integração com autoridades existentes, ausência de envio, cliques locais, troca de conversa, fechamento e histórico literal com emoji e markup.

Testes existentes preservados. Nenhum EditMode/Test Runner foi executado pelo agente.

## M. Verificações

- Assembly-CSharp.csproj: compilação aprovada, 0 warnings, 0 errors.
- Assembly-CSharp-Editor.csproj: compilação aprovada, 0 warnings, 0 errors.
- Logs: `.utmp/emoji-runtime.log` e `.utmp/emoji-editor.log`.
- Nove GUIDs novos verificados únicos em Assets.
- Cmap: 34 emojis, zero caracteres base ausentes; seletores tratados explicitamente.
- Whitespace dos seis arquivos C# envolvidos: sem trailing whitespace.
- Diff dos dois arquivos preexistentes comparado com snapshots anteriores à tarefa; alterações restritas à integração descrita.
- Nenhuma operação Unity GUI, Test Runner ou escrita Git realizada.

## N. Limitações

Sem comprovação visual runtime nesta sessão; fontes importadas/atlas/teclado devem ser conferidos no Unity e no dispositivo. Emojis monocromáticos, Recentes por sessão, sequências Unicode complexas não prometidas. Compilações não equivalem à execução dos testes nem garantem o total verde anterior.

## O. Validação manual mínima

1. Deixar Unity importar assets. Executar EditMode WDChatEmojiPickerTests e WDMatchChatViewTests; depois Run All.
2. Abrir Chat, verificar botão junto ao +, abrir/fechar, categorias e Recentes vazio. Escolher emojis, conferir deduplicação.
3. Em `Boa partida!`, inserir 🔥 após Boa; selecionar `partida` e substituir por ❤️. Conferir caret, texto restante, nenhum envio automático e ausência de quadrados no composer.
4. Enviar pelo botão existente e conferir mensagem no histórico; receber/reabrir histórico com emojis, sem interpretar `<b>literal</b>` como markup.
5. Clicar dentro do picker, fora dele dentro do Chat e no backdrop global. Alternar PARTIDA/CLÃ e fechar/reabrir Chat, preservando rascunhos.
6. Conferir popup em 16:9/20:9 e em Android landscape: toque, teclado, seleção/cursor, retorno de foco e limites do painel. Antes de oferecer iOS, repetir em dispositivo iOS.
