# Chat — migração para Noto colorido

2026-09-26. Este documento substitui a decisão de apresentação monocromática de WDChatEmojiPicker_Implementation.md. Não foi operado Unity GUI/Test Runner.

## A–D. Origem, licença, revisão e arquivos gráficos

- Origem exclusiva: https://github.com/googlefonts/noto-emoji
- Revisão fixa: `e20cbc2bbec1926686be9f9bee7d1d2cfa1fea0e`.
- Caminho original: `2D/svg/emoji_u*.svg`.
- Licença da pasta escolhida: [Apache 2.0](https://github.com/googlefonts/noto-emoji/blob/e20cbc2bbec1926686be9f9bee7d1d2cfa1fea0e/2D/svg/LICENSE). O LICENSE da raiz refere-se à fonte/OFL; não foi usado para classificar os SVGs.
- 34 SVGs oficiais, correspondentes exatamente ao catálogo real de WDChatEmojiPicker. Nenhum third_party/flag, fonte CBDT/CBLC ou arte de terceiros.
- Originais preservados sem mudança, com hashes SHA256 e coordenadas em `Assets/Resources/UI/Chat/Emoji/NotoColor/manifest.json`.
- Mesma pasta contém LICENSE.txt original, Apache-2.0.txt integral, AUTHORS.txt original e NOTICE.txt com origem e descrição da rasterização.
- Derivado: Atlas.png (1024×640), células 128×128, desenhos 120×120, margem transparente de 4 px. Um material ColorSprite.mat referencia explicitamente TextMeshPro/Sprite para inclusão no build.

## E. Dado Unicode e apresentação TMP

`WDChatEmojiColor` configura TMP_SpriteAsset compartilhado em memória, construído do manifest e do atlas/material importados. Não requer TMP Sprite Asset Editor. O ITextPreprocessor de cada texto converte somente as sequências suportadas para aliases de apresentação em áreas privadas Unicode. Não produz tags `<sprite>` nem habilita richText.

O buffer de renderização é separado de TMP_Text.text e TMP_InputField.text. As strings lógicas e os repositories permanecem intocados. Literal `<sprite=0>`, `<b>` e outros textos do usuário continuam literais. Aliases digitados pelo próprio usuário não são aceitos como comandos de apresentação: usam um caractere privado sem sprite, mantendo o dado original.

Aliases BMP F000+índice e suplementares F0000+índice foram auditados contra os três pesos Inter, LiberationSans e Noto. E000 não foi usado porque Inter possui glyphs nessa faixa.

## F. Composer e edição

O catálogo atual contém sequências de um ou dois code units UTF-16. Cada alias tem exatamente o mesmo comprimento do original. Assim, `characterInfo.index` e `stringLength` continuam correspondendo ao texto lógico. Os pares BMP+FE0F de ❤️ e ✌️ tornam-se um sprite suplementar de comprimento dois somente no buffer de renderização.

TMP_InputField continua fazendo cursor, seleção, clipboard, backspace e teclado sobre Unicode. A edição por caracteres renderizados (`isRichTextEditingAllowed=false`, padrão instalado) permite remover cada sequência suportada como uma unidade. Não foi criado input paralelo nem overlay que exigisse sincronização geométrica.

Inserção, seleção, limite, Recentes e callbacks anteriores não foram alterados. O script novo não envia mensagens.

## G–H. Histórico, Preview e picker

WDChatEmojiText.Apply conecta o mesmo sprite asset e processamento ao composer, células do picker e corpos de mensagens. O Preview das conversas privadas recebeu o mesmo tratamento: era o ponto que ainda usava Inter sem fallback.

A medição das bubbles usa `GetPreferredValues(width, height)`, que passa pelo preprocessor no TMP instalado. O overload com string explícita ignora esse processamento e por isso foi substituído somente nesse ponto. Layout geral, painel e colunas não foram alterados.

U+1F601 suportado é resolvido como sprite; não depende de glyph correspondente no Inter nem de supressão de warning. `tintAllSprites=false` preserva as cores oficiais, inclusive em Preview com texto cinza.

## I–J. Fora do catálogo e Noto monocromático

Unicode não mapeado permanece inalterado. Sequências com modificadores de pele ou ZWJ fora do catálogo não são convertidas parcialmente para outro emoji colorido. Não existe download runtime. Fonte Noto monocromática anterior e sua licença permanecem como fallback para caracteres não suportados pelo atlas; glyphs desconhecidos podem continuar sem composição ou sem cobertura.

Para o catálogo, o preprocessor encaminha aos sprites: Noto monocromático não é a autoridade visual. Nenhum asset anterior foi apagado.

## K–L. Recursos, lifecycle e performance

Um atlas importado RGBA, sem mipmaps/compressão destrutiva, compartilhado; aproximadamente 2,5 MiB descomprimidos. Um sprite asset em memória e um material importado. TMP agrupa os emojis no submesh de sprites do próprio texto, sem GameObject/Canvas por emoji. Recursos SVG originais não são carregados individualmente pelo renderer.

Cada preprocessor guarda sua última entrada/saída, reutilizando o resultado quando o texto não muda. O cache de fontes anterior mantém sua propriedade de materiais/atlas; o novo cache destrói somente o TMP_SpriteAsset que criou, nunca o atlas/material importados. Registro de encerramento/reload é refeito quando necessário.

## M. Arquivos desta migração

Criados:
- `Assets/Scripts/UI/WDChatEmojiColor.cs` e meta.
- Pasta `Assets/Resources/UI/Chat/Emoji/NotoColor/`: 34 SVGs, Atlas.png, ColorSprite.mat, manifest.json, quatro documentos de licença/origem, com metas; metas das novas pastas.
- Este relatório.

Alterados:
- `Assets/Scripts/UI/WDChatEmojiText.cs`: conexão com a apresentação colorida.
- `Assets/Scripts/UI/WDMatchChatView.cs`: Preview e medição com preprocessor.
- `Assets/Editor/Tests/WDChatEmojiPickerTests.cs`.
- `Assets/Editor/Tests/WDMatchChatViewTests.cs`.

csproj: inclusão temporária para compilação anterior à importação; restaurada ao estado anterior à tarefa. `.utmp` contém auditoria, snapshots e logs; nenhuma nova dependência runtime.

## N. Testes

Mantidas as regressões anteriores de Unicode, cursor, seleção, limites, Recentes, categorias, fallback e ownership. Atualizado o ciclo de preview scene para medir a apresentação real e verificar também o sprite asset compartilhado.

Adicionados cinco casos:
- cobertura dos 34 sprites, índices UTF-16, texto lógico, atlas compartilhado e picker;
- markup literal, sequências desconhecidas e proteção contra aliases digitados;
- três casos de backspace nativo: 😁, ❤️, ✌️.

Integração existente ampliada: mensagens recebidas e enviadas preservam Unicode, histórico resolve sprites, Preview privado resolve sprites e richText continua false.

## O. Verificações estáticas

- Assembly-CSharp e Assembly-CSharp-Editor compiladas: 0 erros, 0 warnings.
- Catálogo ↔ manifest: 34/34, mesma ordem, nenhuma entrada extra.
- Hashes dos 34 SVGs conferidos com os downloads oficiais auditados.
- Coordenadas do atlas, comprimentos UTF-16 e ausência de colisão nas fontes verificadas.
- 45 GUIDs novos/metas de recursos e script/pastas verificados únicos; shader e textura referenciados pelo material resolvem para metas existentes.
- Diff incremental e whitespace verificados; nenhum repository, dado ou módulo congelado foi editado.
- Atlas inspecionado visualmente como arquivo. Isso não equivale a validação do rendering no Unity.

## P. Passos manuais no Unity

1. Aguardar importação dos novos arquivos; se Auto Refresh estiver desligado, executar Assets → Refresh manualmente. Não é necessário criar Sprite Asset pelo editor.
2. Executar WDChatEmojiPickerTests, depois WDMatchChatViewTests e Run All duas vezes.
3. Em Play Mode, conferir as quatro categorias, Recentes, cores e nitidez. Testar `Boa partida 😁🔥👍`, inclusive texto com `<sprite=0>` e `<b>literal</b>`.
4. Inserir no meio, substituir seleção, apagar 😁/❤️/✌️, copiar/colar e enviar. Conferir que histórico e Preview privado ficam coloridos e a Console não mostra o warning de U+1F601.
5. Fechar/reabrir Chat, alternar PARTIDA/CLÃ/privado e validar rascunhos, seleção e fechamento local.
6. Validar em Android landscape real: teclado, backspace/seleção, 16:9/20:9 e Safe Area. Antes de oferecer iOS, repetir no dispositivo alvo.

## Q. Limitações

Testes e Play Mode não executados pelo agente. Aparência multiplataforma usa os mesmos pixels, mas build/import/shader/teclado ainda requerem validação em dispositivo. Catálogo limitado aos 34 itens; futuras sequências com mais de dois code units exigem estratégia explícita de edição e não serão aceitas silenciosamente pelo mapper. Áreas privadas reservadas não representam conteúdo Unicode público; se digitadas literalmente, permanecem nos dados mas não recebem sprites. Não foi implementado Chat geral nem Hub/Reações.
