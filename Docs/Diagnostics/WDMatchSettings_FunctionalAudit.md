# Configurações da Partida — auditoria funcional

## Autoridades e decisões

| Controle | Antes | Evidência / autoridade | Decisão |
| --- | --- | --- | --- |
| Volume Geral | A: funcional | WDSettingsRepository -> IWDSettingsRuntime -> WDMatchSettingsRuntime -> WDUnitySettingsRuntime -> AudioListener.volume; WDPlayerPrefsSettingsStore | Preservar faixa 0–1, persistência e slider sem callbacks de refresh. |
| Música / SFX | D: sem canais de áudio | Valores existem em WDSettingsData, mas ApplyAudio aplica apenas master. Não há AudioMixer/grupos/fontes categorizadas encontrados no projeto. | Continuam desabilitados. Implementar roteamento real antes de habilitar. |
| Mute geral | E: implementação pequena possível | Mesma autoridade de settings e mesmo AudioListener | MasterMuted separado de MasterVolume; volume efetivo zero quando mutado; desmutar restaura volume configurado. |
| Exibir Emojis | D: sem apresentação ativa correspondente na V2 | WarDominionMatchHUD contém TryShowSocialEmoji, hideSocialEmojis e toast de jogador/espectador. UICompositionRoot retira FinalMatchHUD. A V2 não possui esse fluxo de reactions. | Não reinterpretar como filtro do Chat. Manter OFF/ON desabilitados até existir apresentação/autoridade de reactions da V2. |
| NORMAL / RÁPIDAS | D: sem autoridade distinta adequada | ResolutionSpeed é preferência histórica aplicada pelo HUD legado; ResolutionSequenceController.PlaybackSpeed é a autoridade atual do Fast Forward. | Manter desabilitados. Não conectar este controle de animações de combate ao Fast Forward nem alterar Resolution. |
| Manual | D: sem destino | Nenhuma tela/URL/documento de manual executável encontrado; botão sem callback. | Manter desabilitado até existir destino oficial. |
| Sair | A: saída local confirmada | WDMatchSettingsView confirma uma vez, fecha WDMatchUtilityCommands e invoca WDMatchSetupContext.ExitToHome (Clear + Home). | Preservado; ausência do callback também desabilita solicitação, além da confirmação. |
| Punição de 15 minutos | D: sem autoridade de conta/matchmaking/backend | Saída atual é navegação local, sem serviço de sanções. | Pendente Backend/Multiplayer/Persistence: registrar abandono voluntário e expiração autoritativa de 15 minutos. Não criar bloqueio local fictício. |

## Mute e persistência

- `MasterMuted` usa o JSON existente `WD.Settings.Local.V1`; dados antigos recebem `false`.
- `MasterVolume` não é sobrescrito pelo mute. O slider continua mostrando o valor configurado.
- Alterar volume enquanto mutado atualiza o valor de restauração, mantendo silêncio até desmutar.
- `RestoreAudioDefaults` restaura volume e desativa mute.
- `AudioChanged` atualiza a view sem nova gravação; Bind remove assinatura anterior e OnDestroy libera assinatura.
- A aplicação na Home também respeita o estado persistido pelo mesmo repository/runtime; não foi criada autoridade de áudio paralela.
- Somente o botão mute geral foi habilitado. Música/SFX e seus mutes continuam indisponíveis.
- O estado muted usa preenchimento dourado e contraste escuro no ícone existente. Geometria e assets permanecem iguais.

## Verificação manual

1. Executar diretamente Unity EditMode -> Run All.
2. Abrir Configurações na partida, ajustar Volume Geral e reabrir: mesmo valor.
3. Mutar: áudio efetivo zero, slider preservado, botão selecionado. Desmutar: volume configurado restaurado.
4. Mutar, sair/reabrir a partida: estado persiste. Desmutar ao finalizar a validação.
5. Conferir Música/SFX, emojis, NORMAL/RÁPIDAS e Manual desabilitados; Fast Forward independente.
6. Abandonar -> Cancelar/backdrop permanece na partida; confirmação final retorna à Home uma única vez, sem alegar aplicação de punição.

As verificações automatizadas desta entrega são apenas compilações estáticas. Renderização, áudio audível e EditMode Run All exigem validação no Unity pelo Product Owner.
