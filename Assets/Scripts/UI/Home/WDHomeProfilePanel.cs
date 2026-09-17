using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public enum WDProfileView { None, MyProfile, PublicProfile, History }

[DisallowMultipleComponent]
public sealed class WDHomeProfilePanel : MonoBehaviour
{
    // ============================================================
    // 01. ESTADO E DEPENDÊNCIAS
    // ============================================================

    private WarDominionUITheme theme;
    private WDProfileRepository repository;
    private WDNavigationStack navigation;
    private Action<string, string> openEndpoint;
    private Action identityChanged;
    private RectTransform content;
    private GameObject customizationModal;
    private GameObject nicknameModal;
    private TMP_InputField nicknameInput;
    private WDProfileCustomizationSession customization;
    private WDProfileCosmeticCategory category;
    private WDProfileMode? historyFilter;
    private WDProfileData currentProfile;

    public bool IsOpen => gameObject.activeSelf;
    public bool IsCustomizationOpen => customizationModal != null && customizationModal.activeSelf;
    public bool IsNicknameEditorOpen => nicknameModal != null && nicknameModal.activeSelf;
    public WDProfileView CurrentView { get; private set; }
    public WDProfileData CurrentProfile => currentProfile;
    public string RequestedReplayMatchId { get; private set; }
    public string LastCopiedValue { get; private set; }
    public int VisibleHistoryCount { get; private set; }

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDNavigationStack navigationStack, Action<string, string> endpoint,
        Action onIdentityChanged)
    {
        theme = newTheme;
        repository = newRepository;
        navigation = navigationStack;
        openEndpoint = endpoint;
        identityChanged = onIdentityChanged;

        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = new Color(0.005f, 0.015f, 0.025f, 0.96f);
        blocker.raycastTarget = true;
        content = WDHomeUIFactory.Rect("ProfileContent", transform);
        WDHomeUIFactory.Stretch(content, 54f, 44f, 54f, 44f);
        gameObject.SetActive(false);
    }

    // ============================================================
    // 02. NAVEGAÇÃO CONTEXTUAL
    // ============================================================

    public void OpenMyProfile() => NavigateProfile(repository.LocalProfile, true);

    public void OpenPublicProfile(string playerId)
    {
        WDProfileData profile = repository.Find(playerId);
        if (profile != null)
            NavigateProfile(profile, false);
    }

    public void HideFromNavigation()
    {
        CloseNicknameEditor();
        CloseCustomization();
        CurrentView = WDProfileView.None;
        currentProfile = null;
        gameObject.SetActive(false);
    }

    public bool TryHandleModalBack()
    {
        if (IsNicknameEditorOpen)
        {
            CloseNicknameEditor();
            return true;
        }
        if (IsCustomizationOpen)
        {
            CloseCustomization();
            return true;
        }
        return false;
    }

    private void NavigateProfile(WDProfileData profile, bool own)
    {
        navigation.Push(new WDNavigationContext(
            own ? "profile/me" : $"profile/public/{profile.PlayerId}",
            _ => RenderProfile(profile, own)));
    }

    private void OpenHistory()
    {
        WDProfileData profile = currentProfile;
        navigation.Push(new WDNavigationContext($"profile/{profile.PlayerId}/history",
            state =>
            {
                historyFilter = state as WDProfileMode?;
                RenderHistory(profile);
            }, () => historyFilter));
    }

    // ============================================================
    // 03. MEU PERFIL E PERFIL PÚBLICO
    // ============================================================

    private void RenderProfile(WDProfileData profile, bool own)
    {
        gameObject.SetActive(true);
        currentProfile = profile;
        CurrentView = own ? WDProfileView.MyProfile : WDProfileView.PublicProfile;
        Clear(content);
        BuildHeader(own ? "MEU PERFIL" : "PERFIL");

        RectTransform left = Panel("IdentityPanel", content, new Vector2(0.02f, 0.06f), new Vector2(0.29f, 0.88f));
        AddText(left, "Avatar", "WD", 54f, theme.Acento, new Vector2(0.08f, 0.67f), new Vector2(0.92f, 0.94f), TextAlignmentOptions.Center);
        AddText(left, "Nickname", profile.Nickname, 30f, theme.TextoPrincipal, new Vector2(0.05f, 0.56f), new Vector2(0.95f, 0.66f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(left, "PlayerId", $"ID: {profile.PlayerId}", 15f, theme.TextoSecundario, new Vector2(0.08f, 0.49f), new Vector2(0.72f, 0.56f), TextAlignmentOptions.Center);
        AddButton(left, "CopyPlayerId", "COPIAR", () => Copy(profile.PlayerId), new Vector2(0.70f, 0.49f), new Vector2(0.94f, 0.56f));
        AddText(left, "Division", $"DIVISÃO\n{profile.Division}\n\nCLÃ\n{profile.Clan}", 17f, theme.TextoPrincipal, new Vector2(0.08f, 0.28f), new Vector2(0.92f, 0.47f));

        if (own)
        {
            string equipped = string.Join("\n", profile.Equipped.Select(pair =>
                $"{WDProfileRepository.CategoryLabel(pair.Key)} · {pair.Value.ToUpperInvariant()}"));
            AddText(left, "Equipped", $"PERSONALIZAÇÃO\n{equipped}", 14f, theme.TextoSecundario,
                new Vector2(0.08f, 0.11f), new Vector2(0.92f, 0.29f));
            AddButton(left, "Customize", "PERSONALIZAR PERFIL", OpenCustomization,
                new Vector2(0.08f, 0.02f), new Vector2(0.92f, 0.10f), true);
        }
        else
            BuildSocialActions(left, profile);

        RectTransform stats = Panel("Statistics", content, new Vector2(0.31f, 0.66f), new Vector2(0.98f, 0.88f));
        AddSectionTitle(stats, "ESTATÍSTICAS");
        AddButton(stats, "History", "HISTÓRICO DE PARTIDAS >", OpenHistory,
            new Vector2(0.17f, 0.78f), new Vector2(0.43f, 0.96f));
        if (own || profile.Privacy.StatisticsVisible)
            AddText(stats, "Stats", StatsLine(profile.Statistics), 20f, theme.TextoPrincipal,
                new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.70f), TextAlignmentOptions.Center);
        else
            AddPrivateState(stats, "ESTATÍSTICAS PRIVADAS", "Este jogador optou por não exibir suas estatísticas.");

        RectTransform performance = Panel("Performance", content, new Vector2(0.31f, 0.39f), new Vector2(0.75f, 0.64f));
        AddSectionTitle(performance, "DESEMPENHO POR MODO");
        if (own || profile.Privacy.PerformanceVisible)
            BuildModeStats(performance, profile);
        else
            AddPrivateState(performance, "DESEMPENHO PRIVADO", "Este jogador optou por não exibir seu desempenho por modo.");

        RectTransform division = Panel("CurrentDivision", content, new Vector2(0.77f, 0.39f), new Vector2(0.98f, 0.64f));
        AddSectionTitle(division, "DIVISÃO ATUAL");
        AddText(division, "Value", $"{profile.Division}\n{profile.DivisionPoints:N0} PONTOS", 21f,
            theme.Acento, new Vector2(0.08f, 0.12f), new Vector2(0.92f, 0.72f), TextAlignmentOptions.Center);

        RectTransform achievements = Panel("Achievements", content, new Vector2(0.31f, 0.10f), new Vector2(0.98f, 0.37f));
        AddSectionTitle(achievements, "CONQUISTAS");
        if (own || profile.Privacy.AchievementsVisible)
        {
            AddText(achievements, "Preview", "◎ PRIMEIRA DE MUITAS  1/1     ◇ TÁTICO SUPREMO  15/20 VITÓRIAS\n\n⬡ LEGIONÁRIO FIEL  284/500     ⚔ VETERANO DE GUERRA  CONCLUÍDA",
                16f, theme.TextoPrincipal, new Vector2(0.04f, 0.10f), new Vector2(0.96f, 0.72f), TextAlignmentOptions.Center);
            AddButton(achievements, "All", "VER TODAS >", () => openEndpoint?.Invoke("CONQUISTAS", "Catálogo completo ainda não implementado."),
                new Vector2(0.79f, 0.78f), new Vector2(0.97f, 0.96f));
        }
        else
            AddPrivateState(achievements, "MISSÕES E CONQUISTAS PRIVADAS", "Este jogador optou por não exibir suas missões e conquistas.");
    }

    private void BuildSocialActions(RectTransform left, WDProfileData profile)
    {
        string state = profile.SocialState switch
        {
            WDProfileSocialState.Stranger => "ADICIONAR AMIGO",
            WDProfileSocialState.RequestSent => "SOLICITAÇÃO ENVIADA",
            WDProfileSocialState.Friend => "AMIGO",
            _ => "JOGADOR BLOQUEADO"
        };
        if (profile.SocialState != WDProfileSocialState.Blocked)
            AddButton(left, "Friend", state, () =>
            {
                profile.SendFriendRequest();
                RenderProfile(profile, false);
            }, new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.28f), profile.SocialState == WDProfileSocialState.Stranger);
        AddButton(left, "Chat", "CHAT", () => openEndpoint?.Invoke("CHAT", "Endpoint estrutural. Sistema de mensagens ainda não implementado."),
            new Vector2(0.08f, 0.11f), new Vector2(0.72f, 0.19f));
        AddButton(left, "More", "...", () => openEndpoint?.Invoke("PERFIL", "BLOQUEAR · requer confirmação explícita\nDENUNCIAR · endpoint estrutural"),
            new Vector2(0.75f, 0.11f), new Vector2(0.92f, 0.19f));
    }

    // ============================================================
    // 04. PERSONALIZAÇÃO E NICKNAME
    // ============================================================

    private void OpenCustomization()
    {
        customization = new WDProfileCustomizationSession(repository.LocalProfile, repository.Cosmetics);
        category = WDProfileCosmeticCategory.Avatar;
        RenderCustomization();
    }

    private void RenderCustomization()
    {
        if (customizationModal != null)
            DestroyNow(customizationModal);
        customizationModal = ModalBlocker("ProfileCustomization");
        RectTransform panel = Panel("Panel", customizationModal.transform, new Vector2(0.16f, 0.12f), new Vector2(0.84f, 0.88f));
        AddText(panel, "Title", "PERSONALIZAR PERFIL", 27f, theme.TextoPrincipal,
            new Vector2(0.03f, 0.88f), new Vector2(0.97f, 0.97f)).fontStyle = FontStyles.Bold;
        WDProfileCosmeticCategory[] categories = (WDProfileCosmeticCategory[])Enum.GetValues(typeof(WDProfileCosmeticCategory));
        for (int i = 0; i < categories.Length; i++)
        {
            WDProfileCosmeticCategory captured = categories[i];
            AddButton(panel, $"Tab_{captured}", WDProfileRepository.CategoryLabel(captured), () => { category = captured; RenderCustomization(); },
                new Vector2(0.03f + i * 0.235f, 0.79f), new Vector2(0.255f + i * 0.235f, 0.87f), captured == category);
        }
        RectTransform preview = Panel("Preview", panel, new Vector2(0.03f, 0.17f), new Vector2(0.30f, 0.77f));
        AddText(preview, "PreviewTitle", "PRÉ-VISUALIZAÇÃO", 16f, theme.TextoPrincipal, new Vector2(0.06f, 0.86f), new Vector2(0.94f, 0.96f));
        AddText(preview, "Avatar", $"WD\n\n{repository.LocalProfile.Nickname}\n\n{WDProfileRepository.CategoryLabel(category)}\n{customization.Preview.GetValueOrDefault(category, "-")}",
            21f, theme.Acento, new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.84f), TextAlignmentOptions.Center);
        AddButton(preview, "EditName", "EDITAR NOME", OpenNicknameEditor, new Vector2(0.15f, 0.08f), new Vector2(0.85f, 0.18f));
        RectTransform grid = Panel("Items", panel, new Vector2(0.32f, 0.17f), new Vector2(0.97f, 0.77f));
        WDProfileCosmetic[] items = repository.Cosmetics.Where(item => item.Category == category).ToArray();
        for (int i = 0; i < items.Length; i++)
        {
            WDProfileCosmetic item = items[i];
            int column = i % 4;
            int row = i / 4;
            bool selected = customization.Preview.GetValueOrDefault(category) == item.Id;
            string label = $"{item.Name}\n{(selected ? "✓ EQUIPADO" : item.Owned ? "EQUIPAR" : "VER NA LOJA")}";
            AddButton(grid, item.Id, label, () =>
            {
                customization.PreviewItem(item.Id);
                if (!item.Owned)
                    openEndpoint?.Invoke("LOJA", $"{item.Name} não foi concedido. Loja ainda não implementada.");
                RenderCustomization();
            }, new Vector2(0.03f + column * 0.24f, 0.53f - row * 0.46f),
               new Vector2(0.24f + column * 0.24f, 0.94f - row * 0.46f), selected);
        }
        AddButton(panel, "Cancel", "CANCELAR", CloseCustomization, new Vector2(0.03f, 0.04f), new Vector2(0.30f, 0.13f));
        AddButton(panel, "Save", "SALVAR ALTERAÇÕES", SaveCustomization, new Vector2(0.32f, 0.04f), new Vector2(0.97f, 0.13f), true);
    }

    private void SaveCustomization()
    {
        if (customization.Save())
        {
            CloseCustomization();
            RenderProfile(repository.LocalProfile, true);
        }
        else
            openEndpoint?.Invoke("ITEM NÃO POSSUÍDO", "A pré-visualização não concede nem equipa este item.");
    }

    private void CloseCustomization()
    {
        CloseNicknameEditor();
        if (customizationModal != null)
            DestroyNow(customizationModal);
        customizationModal = null;
        customization = null;
    }

    private void OpenNicknameEditor()
    {
        nicknameModal = ModalBlocker("NicknameEditor");
        RectTransform panel = Panel("Panel", nicknameModal.transform, new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.66f));
        AddText(panel, "Title", "EDITAR NOME", 25f, theme.TextoPrincipal, new Vector2(0.07f, 0.72f), new Vector2(0.93f, 0.93f));
        RectTransform field = Panel("Input", panel, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.66f));
        nicknameInput = field.gameObject.AddComponent<TMP_InputField>();
        TextMeshProUGUI text = AddText(field, "Text", repository.LocalProfile.Nickname, 19f, theme.TextoPrincipal, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = new Vector2(14f, 4f); text.rectTransform.offsetMax = new Vector2(-14f, -4f);
        nicknameInput.textComponent = text;
        nicknameInput.text = repository.LocalProfile.Nickname;
        AddButton(panel, "Cancel", "CANCELAR", CloseNicknameEditor, new Vector2(0.08f, 0.12f), new Vector2(0.45f, 0.32f));
        AddButton(panel, "Confirm", "SALVAR", ConfirmNickname, new Vector2(0.55f, 0.12f), new Vector2(0.92f, 0.32f), true);
    }

    private void ConfirmNickname()
    {
        if (!repository.LocalProfile.TryRename(nicknameInput.text))
        {
            openEndpoint?.Invoke("NOME INVÁLIDO", "Informe um nickname não vazio.");
            return;
        }
        CloseNicknameEditor();
        identityChanged?.Invoke();
        RenderCustomization();
    }

    private void CloseNicknameEditor()
    {
        if (nicknameModal != null)
            DestroyNow(nicknameModal);
        nicknameModal = null;
        nicknameInput = null;
    }

    // ============================================================
    // 05. HISTÓRICO E ENDPOINT DE REPLAY
    // ============================================================

    private void RenderHistory(WDProfileData profile)
    {
        gameObject.SetActive(true);
        currentProfile = profile;
        CurrentView = WDProfileView.History;
        Clear(content);
        BuildHeader("HISTÓRICO DE PARTIDAS");
        RectTransform body = Panel("History", content, new Vector2(0.02f, 0.06f), new Vector2(0.98f, 0.88f));
        bool visible = profile.IsLocalPlayer || profile.Privacy.HistoryVisible;
        if (!visible)
        {
            VisibleHistoryCount = 0;
            AddPrivateState(body, "HISTÓRICO DE PARTIDAS PRIVADO", "Este jogador optou por não exibir seu histórico de partidas.");
            return;
        }
        (string, WDProfileMode?)[] filters = { ("TODAS", null), ("BATALHA", WDProfileMode.Battle),
            ("1V1", WDProfileMode.OneVsOne), ("2V2", WDProfileMode.TwoVsTwo), ("3V3", WDProfileMode.ThreeVsThree) };
        for (int i = 0; i < filters.Length; i++)
        {
            var filter = filters[i];
            AddButton(body, $"Filter_{filter.Item1}", filter.Item1, () => { historyFilter = filter.Item2; RenderHistory(profile); },
                new Vector2(0.02f + i * 0.105f, 0.90f), new Vector2(0.115f + i * 0.105f, 0.98f), historyFilter == filter.Item2);
        }
        IReadOnlyList<WDProfileMatchRecord> rows = WDProfileRepository.FilterHistory(profile, historyFilter);
        VisibleHistoryCount = rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            WDProfileMatchRecord row = rows[i];
            float top = 0.87f - i * 0.078f;
            AddText(body, $"Match_{i}", $"{row.Result}   {row.MapName} · {ModeLabel(row.Mode)}     {row.Participants}     {row.PlayedAt}     PARTIDA: {row.MatchId}",
                13f, theme.TextoPrincipal, new Vector2(0.02f, top - 0.065f), new Vector2(0.79f, top));
            AddButton(body, $"Copy_{i}", "COPIAR ID", () => Copy(row.MatchId), new Vector2(0.80f, top - 0.06f), new Vector2(0.89f, top));
            AddButton(body, $"Replay_{i}", "VER DETALHES", () => RequestReplay(row.MatchId), new Vector2(0.90f, top - 0.06f), new Vector2(0.98f, top), true);
        }
    }

    private void RequestReplay(string matchId)
    {
        RequestedReplayMatchId = matchId;
        openEndpoint?.Invoke("REPLAY AINDA NÃO IMPLEMENTADO", $"MatchId solicitado: {matchId}");
    }

    // ============================================================
    // 06. CONSTRUÇÃO VISUAL REUTILIZÁVEL
    // ============================================================

    private void BuildHeader(string title)
    {
        AddButton(content, "Back", "<  VOLTAR", () => navigation.TryBack(), new Vector2(0.02f, 0.90f), new Vector2(0.13f, 0.98f));
        AddText(content, "ScreenTitle", title, 30f, theme.TextoPrincipal, new Vector2(0.25f, 0.90f), new Vector2(0.75f, 0.99f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
    }

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = theme.BackgroundElevated; image.raycastTarget = true;
        Outline outline = rect.gameObject.AddComponent<Outline>(); outline.effectColor = theme.BorderNeutral;
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Color color,
        Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return label;
    }

    private void AddSectionTitle(Transform parent, string value) =>
        AddText(parent, "SectionTitle", value, 18f, theme.Acento, new Vector2(0.03f, 0.78f), new Vector2(0.70f, 0.96f)).fontStyle = FontStyles.Bold;

    private WDUIPremiumButton AddButton(Transform parent, string name, string label, UnityAction action,
        Vector2 min, Vector2 max, bool primary = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>(); button.Build(theme, label, action);
        if (primary)
        {
            Image image = rect.GetComponent<Image>();
            if (image != null) image.color = theme.Acento;
        }
        return button;
    }

    private void AddPrivateState(Transform parent, string title, string body) =>
        AddText(parent, "PrivateState", $"▣\n{title}\n{body}", 17f, theme.TextoSecundario,
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.78f), TextAlignmentOptions.Center);

    private void BuildModeStats(Transform parent, WDProfileData profile)
    {
        string value = string.Join("\n", profile.ModeStats.Select(pair =>
            $"{ModeLabel(pair.Key),-8}  {StatsLine(pair.Value)}"));
        AddText(parent, "ModeStats", value, 14f, theme.TextoPrincipal,
            new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.74f), TextAlignmentOptions.Center);
    }

    private GameObject ModalBlocker(string name)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, transform); WDHomeUIFactory.Stretch(rect);
        Image image = rect.gameObject.AddComponent<Image>(); image.color = new Color(0f, 0f, 0f, 0.82f); image.raycastTarget = true;
        rect.SetAsLastSibling(); return rect.gameObject;
    }

    private void Copy(string value)
    {
        LastCopiedValue = value;
        GUIUtility.systemCopyBuffer = value;
    }

    private static string StatsLine(WDProfileStats stats) =>
        $"PARTIDAS {stats.Matches}     VITÓRIAS {stats.Wins}     DERROTAS {stats.Losses}     TAXA {stats.WinRate:0.0}%";
    private static string ModeLabel(WDProfileMode mode) => mode switch
    { WDProfileMode.Battle => "BATALHA", WDProfileMode.OneVsOne => "1V1", WDProfileMode.TwoVsTwo => "2V2", _ => "3V3" };
    private static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            DestroyNow(parent.GetChild(i).gameObject);
    }
    private static void DestroyNow(UnityEngine.Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
