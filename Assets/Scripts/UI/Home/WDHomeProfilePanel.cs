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
    private WDSocialRepository social;
    private WDSocialRepository subscribedSocial;
    private WDNavigationStack navigation;
    private Action<string, string> openEndpoint;
    private Action<string> openDirectChat;
    private Action<string, string, Action> confirmSocial;
    private Action identityChanged;
    private RectTransform content;
    private GameObject customizationModal;
    private GameObject nicknameModal;
    private TMP_InputField nicknameInput;
    private WDProfileCustomizationSession customization;
    private WDProfileCosmeticCategory category;
    private WDProfileMode? historyFilter;
    private WDProfileData currentProfile;
    private Action<WDProfileCosmetic> openStoreForCosmetic;
    private WDStoreCatalogConfig storeCatalog;
    private WDProfilePresentation currentPresentation;
    private WDProfileMode selectedMode = WDProfileMode.Battle;

    private static readonly Color FigmaGold = new(0.773f, 0.608f, 0.388f, 1f);
    private static readonly Color FigmaCyan = new(0f, 0.941f, 1f, 1f);
    private static readonly Color FigmaText = new(0.973f, 0.98f, 0.988f, 1f);
    private static readonly Color FigmaMuted = new(0.58f, 0.64f, 0.72f, 1f);
    private static readonly Color FigmaPanel = new(0.031f, 0.067f, 0.114f, 0.95f);
    private static readonly Color FigmaInset = new(0.020f, 0.047f, 0.086f, 0.94f);
    private const string ProfileAssetRoot = "UI/Profile/FigmaPhase6/";
    private const string HomeCanonicalRoot = "UI/Home/FigmaPhase1A/Canonical/";

    public bool IsOpen => gameObject.activeSelf;
    public bool IsCustomizationOpen => customizationModal != null && customizationModal.activeSelf;
    public bool IsNicknameEditorOpen => nicknameModal != null && nicknameModal.activeSelf;
    public WDProfileView CurrentView { get; private set; }
    public WDProfileData CurrentProfile => currentProfile;
    public string RequestedReplayMatchId { get; private set; }
    public string LastCopiedValue { get; private set; }
    public int VisibleHistoryCount { get; private set; }

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDSocialRepository socialRepository, WDNavigationStack navigationStack, Action<string, string> endpoint,
        Action onIdentityChanged, Action<string> directChatAction = null,
        Action<string, string, Action> socialConfirmation = null,
        Action<WDProfileCosmetic> storeAction = null)
    {
        UnsubscribeFromSocial();
        theme = newTheme;
        repository = newRepository;
        social = socialRepository;
        navigation = navigationStack;
        openEndpoint = endpoint;
        openDirectChat = directChatAction;
        confirmSocial = socialConfirmation;
        identityChanged = onIdentityChanged;
        openStoreForCosmetic = storeAction;
        storeCatalog = Resources.Load<WDStoreCatalogConfig>("UI/Home/WDStoreCatalogConfig");
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;
        RawImage background = Texture("ProfileBackground", transform,
            Resources.Load<Texture2D>(ProfileAssetRoot + "ProfileBackground"));
        WDHomeUIFactory.Stretch(background.rectTransform);
        background.uvRect = new Rect(0f, 0f, 1f, 1f);
        background.transform.SetAsFirstSibling();
        Image veil = WDHomeUIFactory.Image("ProfileBackgroundVeil", transform,
            new Color(0.024f, 0.039f, 0.071f, 0.65f));
        WDHomeUIFactory.Stretch(veil.rectTransform);
        veil.raycastTarget = false;
        content = WDHomeUIFactory.Rect("ProfileContent", transform);
        WDHomeUIFactory.Stretch(content);
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        if (social == null || subscribedSocial == social)
            return;
        UnsubscribeFromSocial();
        social.Changed += RefreshCurrentProfile;
        subscribedSocial = social;
    }

    private void OnDisable() => UnsubscribeFromSocial();

    public void Build(WarDominionUITheme newTheme, WDProfileRepository newRepository,
        WDNavigationStack navigationStack, Action<string, string> endpoint,
        Action onIdentityChanged) => Build(newTheme, newRepository,
        new WDSocialRepository(newRepository, Array.Empty<WDSocialMockSeed>()),
        navigationStack, endpoint, onIdentityChanged);

    // ============================================================
    // 02. NAVEGAÇÃO CONTEXTUAL
    // ============================================================

    public void OpenMyProfile() => NavigateProfile(
        repository.Present(repository.LocalProfile.PlayerId, true));

    public void OpenPublicProfile(string playerId)
    {
        WDProfileData profile = repository.Find(playerId);
        if (profile != null)
            NavigateProfile(repository.Present(playerId));
    }

    public void HideFromNavigation()
    {
        CloseNicknameEditor();
        CloseCustomization();
        CurrentView = WDProfileView.None;
        currentProfile = null;
        currentPresentation = null;
        gameObject.SetActive(false);
    }

    private void OnDestroy() => UnsubscribeFromSocial();

    private void UnsubscribeFromSocial()
    {
        if (subscribedSocial == null)
            return;
        subscribedSocial.Changed -= RefreshCurrentProfile;
        subscribedSocial = null;
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

    private void NavigateProfile(WDProfilePresentation presentation)
    {
        WDProfileData profile = presentation.Profile;
        transform.SetAsLastSibling();
        navigation.Push(new WDNavigationContext(
            presentation.IsOwner ? "profile/me" : $"profile/public/{profile.PlayerId}",
            _ => RenderProfile(presentation), exit: HideFromNavigation));
    }

    private void OpenHistory()
    {
        WDProfilePresentation presentation = currentPresentation;
        WDProfileData profile = presentation.Profile;
        navigation.Push(new WDNavigationContext($"profile/{profile.PlayerId}/history",
            state =>
            {
                historyFilter = state as WDProfileMode?;
                RenderHistory(presentation);
            }, () => historyFilter, HideFromNavigation));
    }

    // ============================================================
    // 03. MEU PERFIL E PERFIL PÚBLICO
    // ============================================================

    private void RenderProfile(WDProfilePresentation presentation)
    {
        WDProfileData profile = presentation.Profile;
        bool own = presentation.IsOwner;
        gameObject.SetActive(true);
        currentProfile = profile;
        currentPresentation = presentation;
        CurrentView = own ? WDProfileView.MyProfile : WDProfileView.PublicProfile;
        Clear(content);
        BuildHeader(own ? "MEU PERFIL" : "PERFIL");

        RectTransform left = Panel("IdentityPanel", content,
            new Vector2(0.0333f, 0.0343f), new Vector2(0.2833f, 0.9213f), FigmaGold);
        RectTransform avatarFrame = WDHomeUIFactory.Rect("AvatarFrame", left);
        avatarFrame.anchorMin = avatarFrame.anchorMax = new Vector2(0.5f, 1f);
        avatarFrame.pivot = new Vector2(0.5f, 1f);
        avatarFrame.anchoredPosition = new Vector2(0f, -32f);
        avatarFrame.sizeDelta = new Vector2(240f, 240f);
        Image avatarSurface = avatarFrame.gameObject.AddComponent<Image>();
        avatarSurface.color = new Color(0.02f, 0.047f, 0.086f, 1f);
        WDHomeUIFactory.ApplyRounded(avatarSurface, 12);
        RawImage avatar = Texture("Avatar", avatarFrame,
            WDProfileIdentityVisuals.ResolveAvatar(profile));
        WDHomeUIFactory.Stretch(avatar.rectTransform);
        AspectRatioFitter avatarAspect = avatar.gameObject.AddComponent<AspectRatioFitter>();
        avatarAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        avatarAspect.aspectRatio = avatar.texture != null
            ? (float)avatar.texture.width / avatar.texture.height : 1f;
        AddBorder(avatarFrame, FigmaCyan, 12);
        AddText(left, "Nickname", profile.Nickname, 28f, FigmaText,
            new Vector2(0.08f, 0.625f), new Vector2(0.92f, 0.68f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(left, "PlayerId", $"ID: {profile.PlayerId}", 14f, FigmaMuted,
            new Vector2(0.18f, 0.575f), new Vector2(0.66f, 0.62f), TextAlignmentOptions.Center);
        AddButton(left, "CopyPlayerId", "COPIAR", () => Copy(profile.PlayerId),
            new Vector2(0.66f, 0.577f), new Vector2(0.85f, 0.617f), false, true);
        AddRule(left, 0.557f, FigmaGold, 0.20f);
        AddIdentityFact(left, "Division", ResolveDivisionEmblemTexture(presentation.Division),
            "DIVISÃO", presentation.Division?.DisplayName ?? profile.Division,
            FigmaGold, 0.514f, Color.white, presentation.Division);
        AddIdentityFact(left, "Clan", WDProfileIdentityVisuals.ResolveIcon("icon_clan"),
            "CLÃ", profile.Clan, FigmaCyan, 0.442f, Color.white);

        if (own)
        {
            AddText(left, "PersonalizationTitle", "PERSONALIZAÇÃO", 11f, FigmaGold,
                new Vector2(0.06f, 0.335f), new Vector2(0.94f, 0.365f)).fontStyle = FontStyles.Bold;
            AddRule(left, 0.33f, FigmaGold, 0.18f);
            RectTransform equipped = Panel("Equipped", left,
                new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.32f), new Color(0f, 0.94f, 1f, 0.20f), FigmaInset);
            AddEmptyFrameSummary(equipped, 0.52f, 0.96f);
            AddRule(equipped, 0.50f, FigmaGold, 0.12f);
            AddSkinSummary(equipped, profile, 0.04f, 0.48f);
            AddButton(left, "Customize", "PERSONALIZAR PERFIL", OpenCustomization,
                new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.10f), true);
        }
        else
            BuildSocialActions(left, profile);

        RectTransform stats = Panel("Statistics", content,
            new Vector2(0.30f, 0.763f), new Vector2(0.967f, 0.946f), new Color(0.773f, 0.608f, 0.388f, 0.20f));
        AddSectionTitle(stats, "ESTATÍSTICAS", FigmaGold);
        AddButton(stats, "History", "HISTÓRICO DE PARTIDAS >", OpenHistory,
            new Vector2(0.72f, 0.73f), new Vector2(0.98f, 0.94f), false, true);
        if (presentation.CanShowStatistics)
            BuildMetricRow(stats, presentation.Statistics, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.63f), true);
        else
            AddPrivateState(stats, "ESTATÍSTICAS PRIVADAS", "Este jogador optou por não exibir suas estatísticas.");

        RectTransform performance = Panel("Performance", content,
            new Vector2(0.30f, 0.532f), new Vector2(0.758f, 0.741f), new Color(0f, 0.94f, 1f, 0.20f));
        AddSectionTitle(performance, "DESEMPENHO POR MODO", FigmaCyan);
        if (presentation.CanShowModePerformance)
            BuildModeStats(performance, presentation.ModeStats);
        else
            AddPrivateState(performance, "DESEMPENHO PRIVADO", "Este jogador optou por não exibir seu desempenho por modo.");

        RectTransform division = Panel("CurrentDivision", content,
            new Vector2(0.77f, 0.532f), new Vector2(0.967f, 0.741f), new Color(0.773f, 0.608f, 0.388f, 0.20f));
        AddSectionTitle(division, "DIVISÃO ATUAL", FigmaGold);
        BuildDivisionDisplay(division, presentation.Division, profile.DivisionPoints);

        RectTransform achievements = Panel("Achievements", content,
            new Vector2(0.30f, 0.348f), new Vector2(0.967f, 0.51f), new Color(0.773f, 0.608f, 0.388f, 0.20f));
        AddSectionTitle(achievements, "CONQUISTAS", FigmaGold);
        if (presentation.CanShowMissionsAchievements)
        {
            BuildAchievements(achievements, presentation.Achievements);
            AddButton(achievements, "All", "VER TODAS >", () => openEndpoint?.Invoke("CONQUISTAS", "Catálogo completo ainda não implementado."),
                new Vector2(0.88f, 0.76f), new Vector2(0.98f, 0.95f), false, true);
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
            WDProfileSocialState.RequestReceived => "ACEITAR SOLICITAÇÃO",
            WDProfileSocialState.Friend => "AMIGO",
            WDProfileSocialState.Self => "VOCÊ",
            _ => "JOGADOR BLOQUEADO"
        };
        if (profile.SocialState != WDProfileSocialState.Blocked)
        {
            WDUIPremiumButton friendButton = AddButton(left, "Friend", state, () =>
            {
                if (profile.SocialState == WDProfileSocialState.Stranger)
                    social?.SendFriendRequest(profile.PlayerId);
                else if (profile.SocialState == WDProfileSocialState.RequestReceived)
                    social?.AcceptReceived(profile.PlayerId);
                RenderProfile(repository.Present(profile.PlayerId));
            }, new Vector2(0.06f, 0.115f), new Vector2(0.94f, 0.19f), profile.SocialState == WDProfileSocialState.Stranger);
            friendButton.SetInteractable(profile.SocialState == WDProfileSocialState.Stranger ||
                profile.SocialState == WDProfileSocialState.RequestReceived);
        }
        AddButton(left, "Chat", "CHAT", () =>
        {
            if (openDirectChat != null) openDirectChat(profile.PlayerId);
            else openEndpoint?.Invoke("CHAT", "Endpoint estrutural. Sistema de mensagens ainda não implementado.");
        },
            new Vector2(0.06f, 0.025f), new Vector2(0.72f, 0.10f));
        AddButton(left, "More", "...", () =>
        {
            if (profile.SocialState == WDProfileSocialState.Blocked)
            {
                openEndpoint?.Invoke("JOGADOR BLOQUEADO", "Desbloqueie este jogador pela aba BLOQUEADOS do Hub de Amigos.");
                return;
            }
            if (confirmSocial != null)
                confirmSocial("BLOQUEAR JOGADOR?", $"Bloquear {profile.Nickname}? Amizade e solicitações serão removidas.",
                    () => social?.Block(profile.PlayerId));
            else
                openEndpoint?.Invoke("PERFIL", "BLOQUEAR · requer confirmação explícita\nDENUNCIAR · endpoint estrutural");
        },
            new Vector2(0.75f, 0.025f), new Vector2(0.94f, 0.10f));
    }

    private void RefreshCurrentProfile()
    {
        if (IsOpen && currentProfile != null && CurrentView == WDProfileView.PublicProfile)
            RenderProfile(repository.Present(currentProfile.PlayerId));
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
        customizationModal = ModalBlocker("ProfileCustomization", CloseCustomization);
        RectTransform panel = Panel("Panel", customizationModal.transform,
            new Vector2(0.16f, 0.115f), new Vector2(0.84f, 0.89f), FigmaGold);
        AddText(panel, "Title", "PERSONALIZAR PERFIL", 27f, FigmaText,
            new Vector2(0.03f, 0.90f), new Vector2(0.97f, 0.97f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(panel, "Subtitle", "ESCOLHA SUA APARÊNCIA · PRÉ-VISUALIZE ANTES DE SALVAR", 12f, FigmaMuted,
            new Vector2(0.03f, 0.855f), new Vector2(0.97f, 0.90f), TextAlignmentOptions.Center);
        WDProfileCosmeticCategory[] categories = (WDProfileCosmeticCategory[])Enum.GetValues(typeof(WDProfileCosmeticCategory));
        for (int i = 0; i < categories.Length; i++)
        {
            WDProfileCosmeticCategory captured = categories[i];
            WDUIPremiumButton tab = AddButton(panel, $"Tab_{captured}",
                WDProfileRepository.CategoryLabel(captured),
                () => { category = captured; RenderCustomization(); },
                new Vector2(0.03f + i * 0.235f, 0.785f),
                new Vector2(0.255f + i * 0.235f, 0.845f),
                captured == category, true);
            StyleModeButton(tab, captured == category);
        }
        RectTransform preview = Panel("Preview", panel,
            new Vector2(0.03f, 0.17f), new Vector2(0.30f, 0.765f),
            new Color(FigmaCyan.r, FigmaCyan.g, FigmaCyan.b, 0.20f));
        AddText(preview, "PreviewTitle", "PRÉ-VISUALIZAÇÃO", 14f, FigmaGold,
            new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.97f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        BuildCustomizationPreview(preview);
        AddText(preview, "Nickname", repository.LocalProfile.Nickname, 20f, FigmaText,
            new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.51f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(preview, "Preview", CustomizationPreviewCaption(), 13f, FigmaMuted,
            new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.40f), TextAlignmentOptions.Center);
        AddButton(preview, "EditName", "EDITAR NOME", OpenNicknameEditor,
            new Vector2(0.15f, 0.06f), new Vector2(0.85f, 0.16f), false, true);
        RectTransform grid = Panel("Items", panel,
            new Vector2(0.32f, 0.17f), new Vector2(0.97f, 0.765f),
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f));
        if (category == WDProfileCosmeticCategory.Avatar)
        {
            RectTransform itemContent = CreateCustomizationScroll(grid);
            BuildAvatarCustomization(itemContent);
        }
        else if (category == WDProfileCosmeticCategory.TerritorySkin)
        {
            RectTransform itemContent = CreateCustomizationScroll(grid);
            BuildSkinCustomization(itemContent);
        }
        else
            BuildComingSoon(grid, WDProfileRepository.CategoryLabel(category));
        AddButton(panel, "Cancel", "CANCELAR", CloseCustomization,
            new Vector2(0.03f, 0.045f), new Vector2(0.30f, 0.13f));
        AddButton(panel, "Save", "SALVAR ALTERAÇÕES", SaveCustomization,
            new Vector2(0.32f, 0.045f), new Vector2(0.97f, 0.13f), true);
    }

    private void BuildCustomizationPreview(RectTransform preview)
    {
        RectTransform slot = WDHomeUIFactory.Rect("PreviewVisual", preview);
        slot.anchorMin = slot.anchorMax = new Vector2(0.5f, 0.705f);
        slot.pivot = new Vector2(0.5f, 0.5f);
        slot.anchoredPosition = Vector2.zero;
        slot.sizeDelta = new Vector2(180f, 180f);
        if (category == WDProfileCosmeticCategory.Avatar)
        {
            string avatarId = customization.Preview.GetValueOrDefault(
                WDProfileCosmeticCategory.Avatar, "avatar_01");
            RawImage avatar = Texture("Avatar", slot,
                WDProfileIdentityVisuals.ResolveAvatar(avatarId));
            WDHomeUIFactory.Stretch(avatar.rectTransform);
            AspectRatioFitter aspect = avatar.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = avatar.texture != null
                ? (float)avatar.texture.width / avatar.texture.height : 1f;
            AddBorder(slot, FigmaCyan, 10);
            return;
        }
        if (category == WDProfileCosmeticCategory.TerritorySkin)
        {
            WDProfileSkinDefinition skin = WDProfileSkinCatalog.Find(
                customization.Preview.GetValueOrDefault(category, WDProfileSkinCatalog.Default.Id))
                ?? WDProfileSkinCatalog.Default;
            Image swatch = slot.gameObject.AddComponent<Image>();
            swatch.color = skin.Color;
            WDHomeUIFactory.ApplyRounded(swatch, 18);
            AddBorder(slot, FigmaCyan, 18);
            return;
        }
        Image empty = slot.gameObject.AddComponent<Image>();
        empty.color = FigmaInset;
        WDHomeUIFactory.ApplyRounded(empty, 18);
        AddBorder(slot, new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.35f), 18);
        AddLockIcon(slot, "Lock", new Vector2(0.5f, 0.5f), 32f);
    }

    private static RectTransform CreateCustomizationScroll(RectTransform panel)
    {
        ScrollRect scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform viewport = WDHomeUIFactory.Rect("Viewport", panel);
        WDHomeUIFactory.Stretch(viewport, 10f, 22f, 10f, 10f);
        Image viewportGraphic = viewport.gameObject.AddComponent<Image>();
        viewportGraphic.color = new Color(0f, 0f, 0f, 0.001f);
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform items = WDHomeUIFactory.Rect("ScrollContent", viewport);
        items.anchorMin = new Vector2(0f, 1f);
        items.anchorMax = new Vector2(1f, 1f);
        items.pivot = new Vector2(0.5f, 1f);
        items.anchoredPosition = Vector2.zero;
        items.sizeDelta = new Vector2(0f, 620f);
        scroll.viewport = viewport;
        scroll.content = items;

        RectTransform scrollbarRect = WDHomeUIFactory.Rect("VerticalScrollbar", panel);
        scrollbarRect.anchorMin = new Vector2(1f, 0f);
        scrollbarRect.anchorMax = new Vector2(1f, 1f);
        scrollbarRect.pivot = new Vector2(1f, 0.5f);
        scrollbarRect.offsetMin = new Vector2(-15f, 12f);
        scrollbarRect.offsetMax = new Vector2(-7f, -12f);
        Image track = scrollbarRect.gameObject.AddComponent<Image>();
        track.color = new Color(0.012f, 0.031f, 0.055f, 0.88f);
        WDHomeUIFactory.ApplyRounded(track, 4);
        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;

        RectTransform slidingArea = WDHomeUIFactory.Rect("SlidingArea", scrollbarRect);
        WDHomeUIFactory.Stretch(slidingArea, 1f, 1f, 1f, 1f);
        RectTransform handle = WDHomeUIFactory.Rect("Handle", slidingArea);
        WDHomeUIFactory.Stretch(handle);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.82f);
        WDHomeUIFactory.ApplyRounded(handleImage, 3);
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        return items;
    }

    private string CustomizationPreviewCaption()
    {
        if (category == WDProfileCosmeticCategory.Avatar)
            return "AVATAR\nSELECIONADO";
        if (category == WDProfileCosmeticCategory.TerritorySkin)
        {
            WDProfileSkinDefinition skin = WDProfileSkinCatalog.Find(
                customization.Preview.GetValueOrDefault(category, WDProfileSkinCatalog.Default.Id))
                ?? WDProfileSkinCatalog.Default;
            return "SKIN\n" + TitleCase(skin.DisplayName);
        }
        return WDProfileRepository.CategoryLabel(category) + "\nEM BREVE";
    }

    private void BuildAvatarCustomization(RectTransform grid)
    {
        AddText(grid, "BasicTitle", "AVATARES BÁSICOS", 14f, FigmaCyan,
            new Vector2(0.03f, 0.91f), new Vector2(0.97f, 0.98f)).fontStyle = FontStyles.Bold;
        string equipped = WDProfileIdentityVisuals.EquippedCosmeticId(
            repository.LocalProfile, WDProfileCosmeticCategory.Avatar);
        string selected = customization.Preview.GetValueOrDefault(
            WDProfileCosmeticCategory.Avatar, equipped);
        for (int i = 0; i < 10; i++)
        {
            string id = $"avatar_{i + 1:00}";
            int column = i % 5;
            int row = i / 5;
            float x = 0.03f + column * 0.194f;
            float top = row == 0 ? 0.89f : 0.56f;
            bool isSelected = selected == id;
            string state = isSelected ? equipped == id ? "EQUIPADO" : "SELECIONADO" : string.Empty;
            WDUIPremiumButton button = AddButton(grid, id, state, () =>
            {
                customization.PreviewItem(id);
                RenderCustomization();
            }, new Vector2(x, top - 0.30f), new Vector2(x + 0.18f, top), isSelected, true);
            StyleModeButton(button, isSelected);
            RawImage artwork = Texture("Artwork", button.transform,
                WDProfileIdentityVisuals.ResolveAvatar(id));
            Anchor(artwork.rectTransform, new Vector2(0.10f, 0.20f), new Vector2(0.90f, 0.95f));
            artwork.transform.SetAsFirstSibling();
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                Anchor(label.rectTransform, new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.19f));
        }
        BuildComingSoonStrip(grid, "AVATARES ADQUIRIDOS", 0.015f, 0.22f);
    }

    private void BuildSkinCustomization(RectTransform grid)
    {
        AddText(grid, "BasicTitle", "CORES BÁSICAS", 14f, FigmaCyan,
            new Vector2(0.03f, 0.91f), new Vector2(0.97f, 0.98f)).fontStyle = FontStyles.Bold;
        string equipped = WDProfileIdentityVisuals.EquippedCosmeticId(
            repository.LocalProfile, WDProfileCosmeticCategory.TerritorySkin);
        string selected = customization.Preview.GetValueOrDefault(
            WDProfileCosmeticCategory.TerritorySkin, equipped);
        for (int i = 0; i < WDProfileSkinCatalog.All.Count; i++)
        {
            WDProfileSkinDefinition skin = WDProfileSkinCatalog.All[i];
            int column = i % 5;
            int row = i / 5;
            float x = 0.03f + column * 0.194f;
            float top = row == 0 ? 0.89f : 0.56f;
            bool isSelected = selected == skin.Id;
            string state = isSelected ? equipped == skin.Id ? "EQUIPADO" : "SELECIONADO" : string.Empty;
            WDUIPremiumButton button = AddButton(grid, skin.Id,
                TitleCase(skin.DisplayName) + "\n" + state, () =>
                {
                    customization.PreviewItem(skin.Id);
                    RenderCustomization();
                }, new Vector2(x, top - 0.30f), new Vector2(x + 0.18f, top), false, true);
            button.SetPersistentColors(skin.Color,
                Color.Lerp(skin.Color, Color.white, 0.12f));
            button.SetLabelColor(Color.white);
            Image border = button.transform.Find("Border")?.GetComponent<Image>();
            if (border != null)
                border.color = isSelected ? FigmaCyan : new Color(0.02f, 0.04f, 0.07f, 0.9f);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.color = Color.white;
                Outline outline = label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
                outline.effectDistance = new Vector2(1f, -1f);
            }
        }
        BuildComingSoonStrip(grid, "SKINS ADQUIRIDAS", 0.015f, 0.22f);
    }

    private void BuildComingSoon(RectTransform parent, string section)
    {
        AddText(parent, "Section", section, 14f, FigmaGold,
            new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.88f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddLockIcon(parent, "Lock", new Vector2(0.5f, 0.645f), 28f);
        AddText(parent, "ComingSoon", "EM BREVE", 22f, FigmaGold,
            new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.56f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(parent, "Description", "Novos itens de personalização serão disponibilizados em breve.",
            13f, FigmaMuted, new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.42f), TextAlignmentOptions.Center);
    }

    private void BuildComingSoonStrip(RectTransform parent, string title, float minY, float maxY)
    {
        RectTransform strip = Panel("ComingSoon_" + title, parent,
            new Vector2(0.03f, minY), new Vector2(0.97f, maxY),
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f), FigmaInset);
        AddLockIcon(strip, "Lock", new Vector2(0.065f, 0.5f), 22f);
        AddText(strip, "Title", title + "  ·  EM BREVE", 12f, FigmaGold,
            new Vector2(0.11f, 0.52f), new Vector2(0.96f, 0.88f)).fontStyle = FontStyles.Bold;
        AddText(strip, "Description", "Novos itens de personalização serão disponibilizados em breve.",
            10f, FigmaMuted, new Vector2(0.11f, 0.12f), new Vector2(0.96f, 0.52f));
    }

    private void SaveCustomization()
    {
        if (customization.Save())
        {
            repository.SaveLocalIdentity();
            identityChanged?.Invoke();
            CloseCustomization();
            RenderProfile(repository.Present(repository.LocalProfile.PlayerId, true));
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
        nicknameModal = ModalBlocker("NicknameEditor", CloseNicknameEditor);
        RectTransform panel = Panel("Panel", nicknameModal.transform, new Vector2(0.32f, 0.34f), new Vector2(0.68f, 0.66f));
        AddText(panel, "Title", "EDITAR NOME", 25f, theme.TextoPrincipal, new Vector2(0.07f, 0.72f), new Vector2(0.93f, 0.93f));
        RectTransform field = Panel("Input", panel, new Vector2(0.08f, 0.43f), new Vector2(0.92f, 0.66f));
        nicknameInput = field.gameObject.AddComponent<TMP_InputField>();
        RectTransform viewport = WDHomeUIFactory.Rect("TextViewport", field);
        WDHomeUIFactory.Stretch(viewport, 12f, 4f, 12f, 4f);
        TextMeshProUGUI text = AddText(viewport, "Text", repository.LocalProfile.Nickname,
            19f, theme.TextoPrincipal, Vector2.zero, Vector2.one);
        text.rectTransform.offsetMin = new Vector2(14f, 4f); text.rectTransform.offsetMax = new Vector2(-14f, -4f);
        nicknameInput.textViewport = viewport;
        nicknameInput.textComponent = text;
        nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
        nicknameInput.contentType = TMP_InputField.ContentType.Standard;
        nicknameInput.characterLimit = WDUIContentContracts.NicknameMaxLength;
        WDUITextPolicies.Apply(text, WDUITextPolicy.SingleLineEllipsis);
        nicknameInput.text = repository.LocalProfile.Nickname;
        Image focusBorder = field.Find("Border")?.GetComponent<Image>();
        WDHomeUIFactory.ConfigureEditableInputVisuals(
            nicknameInput, field.GetComponent<Image>(), focusBorder,
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f), FigmaCyan);
        AddButton(panel, "Cancel", "CANCELAR", CloseNicknameEditor, new Vector2(0.08f, 0.12f), new Vector2(0.45f, 0.32f));
        AddButton(panel, "Confirm", "SALVAR", ConfirmNickname, new Vector2(0.55f, 0.12f), new Vector2(0.92f, 0.32f), true);
    }

    private void ConfirmNickname()
    {
        if (!repository.TryRenameLocal(nicknameInput.text))
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

    private void RenderHistory(WDProfilePresentation presentation)
    {
        WDProfileData profile = presentation.Profile;
        gameObject.SetActive(true);
        currentProfile = profile;
        currentPresentation = presentation;
        CurrentView = WDProfileView.History;
        Clear(content);
        BuildHeader("HISTÓRICO DE PARTIDAS");
        RectTransform body = Panel("History", content,
            new Vector2(0.033f, 0.059f), new Vector2(0.967f, 0.935f), new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f));
        if (!presentation.CanShowMatchHistory)
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
            AddButton(body, $"Filter_{filter.Item1}", filter.Item1, () => { historyFilter = filter.Item2; RenderHistory(presentation); },
                new Vector2(0.02f + i * 0.105f, 0.91f), new Vector2(0.115f + i * 0.105f, 0.975f), historyFilter == filter.Item2, true);
        }
        AddText(body, "Recent", "ÚLTIMAS 10 PARTIDAS", 12f, FigmaMuted,
            new Vector2(0.76f, 0.91f), new Vector2(0.98f, 0.975f), TextAlignmentOptions.Right).fontStyle = FontStyles.Bold;
        IReadOnlyList<WDProfileMatchRecord> rows = WDProfileRepository.FilterHistory(presentation, historyFilter);
        VisibleHistoryCount = rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            WDProfileMatchRecord row = rows[i];
            float top = 0.885f - i * 0.081f;
            RectTransform match = Panel($"Match_{i}", body,
                new Vector2(0.02f, top - 0.071f), new Vector2(0.98f, top),
                new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.15f), FigmaInset);
            Color resultColor = row.Result.Contains("VITÓRIA") ? FigmaCyan :
                row.Result.Contains("DERROTA") ? new Color(1f, 0.20f, 0.30f, 1f) : FigmaGold;
            AddText(match, "Result", row.Result, 12f, resultColor,
                new Vector2(0.015f, 0.18f), new Vector2(0.14f, 0.82f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            AddText(match, "Summary", $"{row.MapName} · {ModeLabel(row.Mode)}" +
                (string.IsNullOrEmpty(row.SubmodeId) ? string.Empty : $" · {row.SubmodeId}") +
                $"\n{row.Participants}   {row.PlayedAt}   PARTIDA: {row.MatchId}",
                12f, FigmaText, new Vector2(0.15f, 0.12f), new Vector2(0.68f, 0.88f));
            AddText(match, "Trophies", $"{(row.TrophiesEarned >= 0 ? "+" : string.Empty)}{row.TrophiesEarned} PONTOS", 12f,
                resultColor, new Vector2(0.67f, 0.18f), new Vector2(0.80f, 0.82f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            AddButton(match, $"Copy_{i}", "COPIAR ID", () => Copy(row.MatchId),
                new Vector2(0.805f, 0.16f), new Vector2(0.885f, 0.84f), false, true);
            AddButton(match, $"Replay_{i}", "VER DETALHES", () => RequestReplay(row.MatchId),
                new Vector2(0.89f, 0.16f), new Vector2(0.985f, 0.84f), true, true);
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
        WDHomeBackButtonFactory.BuildCanonical(content, () => navigation.TryBack());
        RectTransform titleGroup = WDHomeUIFactory.Rect("TitleCenter", content);
        titleGroup.anchorMin = titleGroup.anchorMax = new Vector2(0.5f, 0.972f);
        titleGroup.sizeDelta = new Vector2(300f, 52f);
        titleGroup.anchoredPosition = Vector2.zero;
        Image leftLine = WDHomeUIFactory.Image("TitleLeftAccent", titleGroup, FigmaCyan);
        Place(leftLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-118f, 0f), new Vector2(32f, 2f));
        AddText(titleGroup, "ScreenTitle", title, 24f, FigmaText,
            new Vector2(0.17f, 0f), new Vector2(0.83f, 1f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        Image rightLine = WDHomeUIFactory.Image("TitleRightAccent", titleGroup, FigmaCyan);
        Place(rightLine.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(118f, 0f), new Vector2(32f, 2f));
    }

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max) =>
        Panel(name, parent, min, max, new Color(0.773f, 0.608f, 0.388f, 0.20f));

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max,
        Color border, Color? fill = null)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image image = rect.gameObject.AddComponent<Image>(); image.color = fill ?? FigmaPanel; image.raycastTarget = true;
        WDHomeUIFactory.ApplyRounded(image, 12);
        AddBorder(rect, border, 12);
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Color color,
        Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        if (name == "Nickname" || name == "PlayerId")
            WDUITextPolicies.Apply(label, WDUITextPolicy.SingleLineEllipsis);
        else if (name == "Division" || name == "Equipped" || name == "Preview")
            WDUITextPolicies.Apply(label, WDUITextPolicy.BoundedWrap, 6);
        return label;
    }

    private void AddSectionTitle(Transform parent, string value, Color color) =>
        AddText(parent, "SectionTitle", value, 18f, color,
            new Vector2(0.02f, 0.73f), new Vector2(0.72f, 0.96f)).fontStyle = FontStyles.Bold;

    private WDUIPremiumButton AddButton(Transform parent, string name, string label, UnityAction action,
        Vector2 min, Vector2 max, bool primary = false, bool compact = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>(); button.Build(theme, label, action);
        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.color = primary ? FigmaGold : FigmaInset;
            WDHomeUIFactory.ApplyRounded(image, compact ? 7 : 9);
        }
        TextMeshProUGUI text = rect.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.color = primary ? new Color(0.031f, 0.067f, 0.114f, 1f) : FigmaGold;
            text.fontSize = compact ? 11f : 14f;
            text.fontStyle = FontStyles.Bold;
        }
        if (primary)
        {
            button.SetPersistentColors(FigmaGold,
                Color.Lerp(FigmaGold, Color.white, 0.12f));
            button.SetLabelColor(new Color(0.031f, 0.067f, 0.114f, 1f));
        }
        AddBorder(rect, primary ? FigmaGold : new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.55f), compact ? 7 : 9);
        return button;
    }

    private void AddPrivateState(Transform parent, string title, string body)
    {
        AddLockIcon(parent, "PrivateLock", new Vector2(0.5f, 0.68f), 24f);
        AddText(parent, "PrivateState", $"{title}\n{body}", 17f, FigmaMuted,
            new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.60f), TextAlignmentOptions.Center);
    }

    // ============================================================
    // 07. COMPONENTES VISUAIS DO PERFIL FIGMA FASE 6
    // ============================================================

    private void AddIdentityFact(Transform parent, string name, Texture2D iconTexture,
        string eyebrow, string value, Color accent, float centerY, Color iconColor,
        WDDivisionDefinition division = null)
    {
        RectTransform iconHolder = WDHomeUIFactory.Rect(name + "IconHolder", parent);
        iconHolder.anchorMin = iconHolder.anchorMax = new Vector2(0.11f, centerY);
        iconHolder.pivot = new Vector2(0.5f, 0.5f);
        iconHolder.anchoredPosition = new Vector2(0f, -3f);
        float iconSide = name == "Division" ? 56f : 34f;
        iconHolder.sizeDelta = new Vector2(iconSide, iconSide);
        RawImage icon = Texture(name + "Icon", iconHolder, iconTexture);
        icon.color = iconColor;
        WDHomeUIFactory.Stretch(icon.rectTransform);
        if (division != null)
        {
            WDDivisionVisuals.ApplyCalibration(icon.rectTransform, division, iconSide);
        }
        else
        {
            AspectRatioFitter aspect = icon.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = icon.texture != null
                ? (float)icon.texture.width / icon.texture.height : 1f;
        }
        const float textLeft = 0.19f;
        AddText(parent, name + "Eyebrow", eyebrow, 10f, FigmaMuted,
            new Vector2(textLeft, centerY), new Vector2(0.92f, centerY + 0.028f));
        TextMeshProUGUI label = AddText(parent, name, value, 15f, accent,
            new Vector2(textLeft, centerY - 0.028f), new Vector2(0.92f, centerY));
        label.fontStyle = FontStyles.Bold;
    }

    private void AddEmptyFrameSummary(Transform parent, float minY, float maxY)
    {
        RectTransform lockTile = Panel("FrameEmpty", parent,
            new Vector2(0.02f, minY + 0.05f), new Vector2(0.24f, maxY - 0.05f),
            new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f), FigmaInset);
        AddLockIcon(lockTile, "Lock", new Vector2(0.5f, 0.5f), 22f);
        AddText(parent, "FrameTitle", "MOLDURA", 13f, FigmaText,
            new Vector2(0.28f, minY + 0.19f), new Vector2(0.96f, maxY - 0.06f)).fontStyle = FontStyles.Bold;
        AddText(parent, "FrameState", "Nenhuma moldura", 11f, FigmaMuted,
            new Vector2(0.28f, minY + 0.02f), new Vector2(0.96f, minY + 0.20f));
    }

    private void AddSkinSummary(Transform parent, WDProfileData profile, float minY, float maxY)
    {
        string skinId = WDProfileIdentityVisuals.EquippedCosmeticId(
            profile, WDProfileCosmeticCategory.TerritorySkin);
        WDProfileSkinDefinition skin = WDProfileSkinCatalog.Find(skinId) ?? WDProfileSkinCatalog.Default;
        Image swatch = WDHomeUIFactory.Image("SkinSwatch", parent, skin.Color);
        Anchor(swatch.rectTransform, new Vector2(0.02f, minY + 0.05f), new Vector2(0.24f, maxY - 0.05f));
        WDHomeUIFactory.ApplyRounded(swatch, 6);
        AddBorder(swatch.rectTransform, new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.35f), 6);
        AddText(parent, "SkinTitle", "SKIN", 13f, FigmaText,
            new Vector2(0.28f, minY + 0.19f), new Vector2(0.96f, maxY - 0.06f)).fontStyle = FontStyles.Bold;
        AddText(parent, "SkinState", TitleCase(skin.DisplayName), 11f, FigmaMuted,
            new Vector2(0.28f, minY + 0.02f), new Vector2(0.96f, minY + 0.20f));
    }

    private void BuildMetricRow(Transform parent, WDProfileStats stats,
        Vector2 min, Vector2 max, bool large, bool cyanBorders = false)
    {
        (string label, string value, Color color)[] metrics = {
            ("PARTIDAS", stats.Matches.ToString("N0"), FigmaText),
            ("VITÓRIAS", stats.Wins.ToString("N0"), FigmaCyan),
            ("DERROTAS", stats.Losses.ToString("N0"), FigmaText),
            ("TAXA DE VITÓRIA", stats.WinRate.ToString("0.0") + "%", FigmaGold) };
        float gap = 0.012f;
        float width = (max.x - min.x - gap * 3f) / 4f;
        for (int i = 0; i < metrics.Length; i++)
        {
            float x = min.x + i * (width + gap);
            RectTransform box = Panel("Metric_" + i, parent,
                new Vector2(x, min.y), new Vector2(x + width, max.y),
                cyanBorders
                    ? new Color(FigmaCyan.r, FigmaCyan.g, FigmaCyan.b, 0.20f)
                    : new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f),
                FigmaInset);
            AddText(box, "Label", metrics[i].label, large ? 11f : 9f, FigmaMuted,
                new Vector2(0.06f, 0.55f), new Vector2(0.94f, 0.90f)).fontStyle = FontStyles.Bold;
            AddText(box, "Value", metrics[i].value, large ? 26f : 17f, metrics[i].color,
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.58f)).fontStyle = FontStyles.Bold;
        }
    }

    private void BuildDivisionDisplay(RectTransform parent, WDDivisionDefinition division, int points)
    {
        RectTransform medal = WDHomeUIFactory.Rect("DivisionMedallion", parent);
        medal.anchorMin = medal.anchorMax = new Vector2(0.22f, 0.40f);
        medal.pivot = new Vector2(0.5f, 0.5f);
        medal.anchoredPosition = Vector2.zero;
        medal.sizeDelta = new Vector2(96f, 96f);
        RawImage gold = Texture("Gold", medal, WDDivisionVisuals.ResolveMedallion());
        WDHomeUIFactory.Stretch(gold.rectTransform);
        Texture2D emblemTexture = ResolveDivisionEmblemTexture(division);
        RawImage emblem = Texture("Emblem", medal, emblemTexture);
        Anchor(emblem.rectTransform, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.93f));
        WDDivisionVisuals.ApplyCalibration(emblem.rectTransform, division, 96f);
        AddText(parent, "DivisionName", division?.DisplayName ?? string.Empty, 18f, FigmaGold,
            new Vector2(0.39f, 0.35f), new Vector2(0.96f, 0.59f)).fontStyle = FontStyles.Bold;
        AddText(parent, "DivisionPoints", $"{points:N0} PONTOS", 12f, FigmaText,
            new Vector2(0.39f, 0.19f), new Vector2(0.96f, 0.36f));
    }

    private void BuildAchievements(RectTransform parent, IReadOnlyList<WDProfileAchievement> achievements)
    {
        int count = Mathf.Min(4, achievements.Count);
        for (int i = 0; i < count; i++)
        {
            WDProfileAchievement item = achievements[i];
            float minX = 0.02f + i * 0.245f;
            RectTransform card = Panel("Achievement_" + i, parent,
                new Vector2(minX, 0.08f), new Vector2(minX + 0.23f, 0.67f),
                new Color(FigmaCyan.r, FigmaCyan.g, FigmaCyan.b, 0.20f), FigmaInset);
            RawImage icon = Texture("Icon", card,
                WDProfileIdentityVisuals.ResolveAchievement(item.Id));
            icon.color = FigmaCyan;
            Anchor(icon.rectTransform, new Vector2(0.07f, 0.23f), new Vector2(0.24f, 0.77f));
            AddText(card, "Title", item.Title, 12f, FigmaText,
                new Vector2(0.29f, 0.47f), new Vector2(0.97f, 0.79f)).fontStyle = FontStyles.Bold;
            AddText(card, "Progress", item.Progress, 10f, FigmaCyan,
                new Vector2(0.29f, 0.16f), new Vector2(0.97f, 0.48f));
        }
    }

    private void AddRule(Transform parent, float y, Color color, float alpha)
    {
        Image line = WDHomeUIFactory.Image("Rule", parent,
            new Color(color.r, color.g, color.b, alpha));
        Anchor(line.rectTransform, new Vector2(0.06f, y), new Vector2(0.94f, y + 0.0015f));
    }

    private RawImage Texture(string name, Transform parent, Texture2D texture)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent);
        RawImage image = rect.gameObject.AddComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
        return image;
    }

    private static Texture2D ResolveDivisionEmblemTexture(WDDivisionDefinition division)
        => WDDivisionVisuals.ResolveEmblem(division);

    private static void Anchor(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static void AddBorder(RectTransform target, Color color, int radius) =>
        WDHomeUIFactory.AddRoundedBorder(target, color, radius);

    private static void AddLockIcon(Transform parent, string name, Vector2 anchor, float side)
    {
        RectTransform holder = WDHomeUIFactory.Rect(name, parent);
        holder.anchorMin = holder.anchorMax = anchor;
        holder.pivot = new Vector2(0.5f, 0.5f);
        holder.anchoredPosition = Vector2.zero;
        holder.sizeDelta = new Vector2(side, side);
        RawImage icon = holder.gameObject.AddComponent<RawImage>();
        icon.texture = WDProfileIdentityVisuals.ResolveLockIcon();
        icon.color = FigmaMuted;
        icon.raycastTarget = false;
        icon.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = size;
    }

    private static string TitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return string.Join(" ", value.ToLowerInvariant().Split(' ')
            .Select(word => string.IsNullOrEmpty(word) ? word :
                char.ToUpperInvariant(word[0]) + word.Substring(1)));
    }

    private void BuildModeStats(Transform parent, IReadOnlyDictionary<WDProfileMode, WDProfileStats> modeStats)
    {
        (string label, WDProfileMode mode)[] tabs = {
            ("BATALHA", WDProfileMode.Battle), ("1V1", WDProfileMode.OneVsOne),
            ("2V2", WDProfileMode.TwoVsTwo), ("3V3", WDProfileMode.ThreeVsThree) };
        for (int i = 0; i < tabs.Length; i++)
        {
            WDProfileMode captured = tabs[i].mode;
            WDUIPremiumButton tab = AddButton(parent, "Mode_" + tabs[i].label, tabs[i].label,
                () => { selectedMode = captured; RenderProfile(currentPresentation); },
                new Vector2(0.02f + i * 0.09f, 0.48f), new Vector2(0.10f + i * 0.09f, 0.67f),
                selectedMode == captured, true);
            StyleModeButton(tab, selectedMode == captured);
        }
        WDProfileStats selected = modeStats.TryGetValue(selectedMode, out WDProfileStats value)
            ? value : new WDProfileStats(0, 0);
        BuildMetricRow(parent, selected, new Vector2(0.02f, 0.08f),
            new Vector2(0.98f, 0.42f), false, true);
    }

    private static void StyleModeButton(WDUIPremiumButton button, bool selected)
    {
        if (button == null) return;
        Image surface = button.GetComponent<Image>();
        if (surface != null)
            surface.color = selected
                ? new Color(FigmaCyan.r, FigmaCyan.g, FigmaCyan.b, 0.08f)
                : FigmaInset;
        Image border = button.transform.Find("Border")?.GetComponent<Image>();
        if (border != null)
            border.color = selected
                ? FigmaCyan
                : new Color(FigmaGold.r, FigmaGold.g, FigmaGold.b, 0.20f);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = selected ? FigmaCyan : FigmaMuted;
    }

    private GameObject ModalBlocker(string name, UnityAction outsideAction)
    {
        RectTransform root = WDHomeUIFactory.Rect(name, transform);
        WDHomeUIFactory.Stretch(root);
        RectTransform outside = WDHomeUIFactory.Rect("OutsideDismiss", root);
        WDHomeUIFactory.Stretch(outside);
        Image image = outside.gameObject.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.82f);
        image.raycastTarget = true;
        Button button = outside.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(outsideAction);
        root.SetAsLastSibling();
        return root.gameObject;
    }

    private void Copy(string value)
    {
        LastCopiedValue = value;
        GUIUtility.systemCopyBuffer = value;
    }

    private static string StatsLine(WDProfileStats stats) =>
        $"PARTIDAS {stats.Matches}     VITÓRIAS {stats.Wins}     DERROTAS {stats.Losses}" +
        (stats.Draws > 0 ? $"     EMPATES {stats.Draws}" : string.Empty) +
        $"     TAXA {stats.WinRate:0.0}%";
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
