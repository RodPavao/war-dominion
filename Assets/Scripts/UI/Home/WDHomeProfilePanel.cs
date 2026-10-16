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
    private Func<WDArmoryProfile> economyProvider;
    private Action openNotifications;
    private WDStoreCatalogConfig storeCatalog;
    private WDProfilePresentation currentPresentation;
    private WDProfileMode selectedMode = WDProfileMode.Battle;
    private RectTransform responsiveShell;
    private WDHomeSecondaryScreenFrame responsiveFrame;
    private RectTransform chrome;

    // 01.1. Os tokens do Figma são sRGB. A conversão explícita evita que o
    // Color Space Linear clareie os navies e intensifique o dourado no Canvas.
    private static readonly Color FigmaGold = Srgb(197, 155, 99);
    private static readonly Color FigmaCyan = Srgb(0, 240, 255);
    private static readonly Color FigmaText = Srgb(248, 250, 252);
    private static readonly Color FigmaMuted = Srgb(148, 163, 184);
    private static readonly Color FigmaInk = Srgb(8, 17, 29);
    private static readonly Color FigmaBackdrop = Srgb(6, 10, 18, .65f);
    private static readonly Color FigmaPanel = Srgb(8, 17, 29, .95f);
    private static readonly Color FigmaInset = Srgb(5, 12, 22);
    private static readonly Color FigmaInsetSoft = Srgb(5, 12, 22, .902f);
    private static readonly Color FigmaSelectedSurface = Srgb(5, 12, 22, .50f);
    private static readonly Color FigmaElevated = Srgb(10, 22, 36);
    private static readonly Color FigmaHeader = Srgb(7, 10, 15, .84f);
    private static readonly Color FigmaButton = Srgb(17, 24, 32, .90f);
    private static readonly Color FigmaModalBackdrop = new(0f, 0f, 0f, 0.70f);
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
        Action<WDProfileCosmetic> storeAction = null,
        Func<WDArmoryProfile> economyStateProvider = null,
        Action notificationAction = null,
        Texture2D profileBackgroundTexture = null)
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
        economyProvider = economyStateProvider;
        openNotifications = notificationAction;
        storeCatalog = Resources.Load<WDStoreCatalogConfig>("UI/Home/WDStoreCatalogConfig");
        Image blocker = gameObject.AddComponent<Image>();
        blocker.color = Color.black;
        blocker.raycastTarget = true;
        // 01.2. A textura serializada pela Home é a única autoridade do background.
        RawImage background = Texture("ProfileBackground", transform, profileBackgroundTexture);
        background.rectTransform.anchorMin = background.rectTransform.anchorMax = new Vector2(.5f, .5f);
        background.rectTransform.pivot = new Vector2(.5f, .5f);
        background.rectTransform.anchoredPosition = Vector2.zero;
        background.uvRect = new Rect(0f, 0f, 1f, 1f);
        // 01.3. A arte 16:9 usa cover no Edge e no ultrawide; isso preserva a proporção
        // original e recorta somente o excedente, como o background full bleed do Figma.
        AspectRatioFitter backgroundCover = background.gameObject.AddComponent<AspectRatioFitter>();
        backgroundCover.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        backgroundCover.aspectRatio = profileBackgroundTexture != null
            ? (float)profileBackgroundTexture.width / profileBackgroundTexture.height
            : 16f / 9f;
        background.transform.SetAsFirstSibling();
        Image veil = WDHomeUIFactory.Image("ProfileBackgroundVeil", transform, FigmaBackdrop);
        WDHomeUIFactory.Stretch(veil.rectTransform);
        veil.raycastTarget = false;
        // 01.1. Header e conteúdo usam a mesma referência lógica responsiva.
        responsiveShell = WDHomeUIFactory.Rect("ProfileResponsiveShell", transform);
        responsiveFrame = WDHomeSecondaryScreenFrame.Configure(responsiveShell);
        chrome = WDHomeUIFactory.Rect("ProfileChrome", responsiveShell);
        WDHomeUIFactory.Stretch(chrome);
        content = WDHomeUIFactory.Rect("ProfileContent", responsiveShell);
        WDHomeUIFactory.Stretch(content);
        FitSafeArea();
        gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        FitSafeArea();
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
            new Vector2(0.016667f, 0.059259f), new Vector2(0.266667f, 0.896296f),
            WithAlpha(FigmaGold, .24f), WithAlpha(FigmaInk, .949f), 16);
        RectTransform avatarFrame = WDHomeUIFactory.Rect("AvatarFrame", left);
        avatarFrame.anchorMin = avatarFrame.anchorMax = new Vector2(0.5f, 1f);
        avatarFrame.pivot = new Vector2(0.5f, 1f);
        avatarFrame.anchoredPosition = new Vector2(0f, -32f);
        avatarFrame.sizeDelta = new Vector2(240f, 240f);
        Image avatarSurface = avatarFrame.gameObject.AddComponent<Image>();
        avatarSurface.color = FigmaInset;
        WDHomeUIFactory.ApplyRounded(avatarSurface, 12);
        RawImage avatar = Texture("Avatar", avatarFrame,
            WDProfileIdentityVisuals.ResolveAvatar(profile));
        WDHomeUIFactory.Stretch(avatar.rectTransform);
        AspectRatioFitter avatarAspect = avatar.gameObject.AddComponent<AspectRatioFitter>();
        avatarAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        avatarAspect.aspectRatio = avatar.texture != null
            ? (float)avatar.texture.width / avatar.texture.height : 1f;
        // 03.1. O avatar é a exceção documentada de 2 px; demais traços do
        // Perfil permanecem no componente fino de 1 px.
        WDHomeUIFactory.AddRoundedBorder(avatarFrame, FigmaCyan, 12);
        AddText(left, "Nickname", profile.Nickname, 28f, FigmaText,
            new Vector2(0.08f, 0.625f), new Vector2(0.92f, 0.68f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(left, "PlayerId", $"ID: {profile.PlayerId}", 14f, FigmaMuted,
            new Vector2(0.18f, 0.575f), new Vector2(0.66f, 0.62f), TextAlignmentOptions.Center);
        WDUIPremiumButton copyPlayerId = AddButton(left, "CopyPlayerId", "COPIAR",
            () => Copy(profile.PlayerId), new Vector2(0.66f, 0.577f),
            new Vector2(0.85f, 0.617f), false, true);
        StyleCyanSecondaryButton(copyPlayerId);
        AddRule(left, 0.557f, FigmaGold, 0.20f);
        // 02.1. Faixas verticais independentes preservam a sequência Identidade > Divisão > Clã > Personalização.
        AddIdentityFact(left, "Division", ResolveDivisionEmblemTexture(presentation.Division),
            "DIVISÃO", presentation.Division?.DisplayName ?? profile.Division,
            theme.MasterGold, 0.525f, Color.white, presentation.Division);
        AddIdentityFact(left, "Clan", WDProfileIdentityVisuals.ResolveIcon("icon_clan"),
            "CLÃ", profile.Clan, FigmaCyan, 0.475f, Color.white);

        if (own)
        {
            AddText(left, "PersonalizationTitle", "PERSONALIZAÇÃO", 11f, theme.MasterGold,
                new Vector2(0.06f, 0.405f), new Vector2(0.94f, 0.425f)).fontStyle = FontStyles.Bold;
            AddRule(left, 0.397f, theme.MasterGold, 0.18f);
            RectTransform equipped = Panel("Equipped", left,
                new Vector2(0.06f, 0.21f), new Vector2(0.94f, 0.385f),
                WithAlpha(FigmaCyan, .20f), FigmaElevated, 10);
            AddFrameSummary(equipped, profile, 0.52f, 0.96f);
            AddRule(equipped, 0.50f, theme.MasterGold, 0.12f);
            AddSkinSummary(equipped, profile, 0.04f, 0.48f);
            AddButton(left, "Customize", "PERSONALIZAR PERFIL", OpenCustomization,
                new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.10f), true);
        }
        else
            BuildSocialActions(left, profile);

        RectTransform stats = Panel("Statistics", content,
            new Vector2(0.283333f, 0.725926f), new Vector2(0.983333f, 0.896296f), CanonicalGoldBorder());
        AddSectionTitle(stats, "ESTATÍSTICAS", theme.MasterGold);
        WDUIPremiumButton historyButton = AddButton(stats, "History", "HISTÓRICO DE PARTIDAS >", OpenHistory,
            new Vector2(0.13f, 0.73f), new Vector2(0.31f, 0.94f), false, true);
        StyleCanonicalGoldBorder(historyButton);
        if (presentation.CanShowStatistics)
            BuildMetricRow(stats, presentation.Statistics, new Vector2(0.02f, 0.08f), new Vector2(0.98f, 0.63f), true);
        else
            AddPrivateState(stats, "ESTATÍSTICAS PRIVADAS", "Este jogador optou por não exibir suas estatísticas.");

        RectTransform performance = Panel("Performance", content,
            new Vector2(0.283333f, 0.512963f), new Vector2(0.758333f, 0.701852f), WithAlpha(FigmaCyan, .20f));
        AddSectionTitle(performance, "DESEMPENHO POR MODO", FigmaCyan);
        if (presentation.CanShowModePerformance)
            BuildModeStats(performance, presentation.ModeStats);
        else
            AddPrivateState(performance, "DESEMPENHO PRIVADO", "Este jogador optou por não exibir seu desempenho por modo.");

        RectTransform division = Panel("CurrentDivision", content,
            new Vector2(0.770833f, 0.512963f), new Vector2(0.983333f, 0.701852f), CanonicalGoldBorder());
        AddSectionTitle(division, "DIVISÃO ATUAL", theme.MasterGold);
        BuildDivisionDisplay(division, presentation.Division, profile.DivisionPoints);

        RectTransform achievements = Panel("Achievements", content,
            new Vector2(0.283333f, 0.338889f), new Vector2(0.983333f, 0.490741f), CanonicalGoldBorder());
        AddSectionTitle(achievements, "CONQUISTAS", theme.MasterGold);
        if (presentation.CanShowMissionsAchievements)
        {
            BuildAchievements(achievements, presentation.Achievements);
            WDUIPremiumButton allButton = AddButton(achievements, "All", "VER TODAS >", () => openEndpoint?.Invoke("CONQUISTAS", "Catálogo completo ainda não implementado."),
                new Vector2(0.88f, 0.76f), new Vector2(0.98f, 0.95f), false, true);
            StyleCanonicalGoldBorder(allButton);
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
        // 04.1. O modal ocupa a área 1320 x 912 do master 1920 x 1080.
        RectTransform panel = Panel("Panel", customizationModal.transform,
            new Vector2(0.15625f, 0.07778f), new Vector2(0.84375f, 0.92222f),
            FigmaGold, null, 16);
        AddText(panel, "Title", "PERSONALIZAR PERFIL", 28f, FigmaText,
            new Vector2(0.021f, 0.91f), new Vector2(0.979f, 0.97f)).fontStyle = FontStyles.Bold;
        AddText(panel, "Subtitle", "Gerencie seus itens visuais do perfil com uma pré-visualização clara e uma grade organizada de itens disponíveis.", 14f, FigmaMuted,
            new Vector2(0.021f, 0.865f), new Vector2(0.979f, 0.91f));
        WDProfileCosmeticCategory[] categories = (WDProfileCosmeticCategory[])Enum.GetValues(typeof(WDProfileCosmeticCategory));
        for (int i = 0; i < categories.Length; i++)
        {
            WDProfileCosmeticCategory captured = categories[i];
            WDUIPremiumButton tab = AddButton(panel, $"Tab_{captured}",
                WDProfileRepository.CategoryLabel(captured),
                () => { category = captured; RenderCustomization(); },
                new Vector2(0.021f + i * 0.241f, 0.812f),
                new Vector2(0.253f + i * 0.241f, 0.873f),
                captured == category, true);
            StyleModeButton(tab, captured == category);
        }
        RectTransform preview = Panel("Preview", panel,
            new Vector2(0.021f, 0.118f), new Vector2(0.294f, 0.785f),
            WithAlpha(FigmaCyan, .20f));
        AddText(preview, "PreviewTitle", "PRÉ-VISUALIZAÇÃO", 14f, theme.MasterGold,
            new Vector2(0.06f, 0.91f), new Vector2(0.94f, 0.97f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        BuildCustomizationPreview(preview);
        AddText(preview, "Nickname", repository.LocalProfile.Nickname, 20f, FigmaText,
            new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.51f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(preview, "Preview", CustomizationPreviewCaption(), 13f, FigmaMuted,
            new Vector2(0.08f, 0.20f), new Vector2(0.92f, 0.40f), TextAlignmentOptions.Center);
        AddButton(preview, "EditName", "EDITAR NOME", OpenNicknameEditor,
            new Vector2(0.15f, 0.06f), new Vector2(0.85f, 0.16f), false, true);
        RectTransform grid = Panel("Items", panel,
            new Vector2(0.312f, 0.118f), new Vector2(0.979f, 0.785f),
            WithAlpha(FigmaGold, .20f));
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
        {
            RectTransform itemContent = CreateCustomizationScroll(grid);
            BuildCosmeticCustomization(itemContent, category);
        }
        AddButton(panel, "Cancel", "CANCELAR", CloseCustomization,
            new Vector2(0.021f, 0.03f), new Vector2(0.263f, 0.091f));
        AddButton(panel, "Save", "SALVAR ALTERAÇÕES", SaveCustomization,
            new Vector2(0.275f, 0.03f), new Vector2(0.979f, 0.091f), true);
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
        string itemId = customization.Preview.GetValueOrDefault(category, string.Empty);
        Texture2D thumbnail = WDProfileIdentityVisuals.ResolveCosmeticThumbnail(category, itemId);
        Image empty = slot.gameObject.AddComponent<Image>();
        empty.color = FigmaInset;
        WDHomeUIFactory.ApplyRounded(empty, 18);
        if (thumbnail != null)
        {
            RawImage artwork = Texture("Artwork", slot, thumbnail);
            Anchor(artwork.rectTransform, new Vector2(.06f, .06f), new Vector2(.94f, .94f));
            AspectRatioFitter aspect = artwork.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = (float)thumbnail.width / thumbnail.height;
        }
        else if (category == WDProfileCosmeticCategory.Frame)
            AddBorder(slot, WithAlpha(FigmaGold, .75f), 18);
        else
        {
            Image effect = WDHomeUIFactory.Image("Effect", slot,
                WithAlpha(FigmaCyan, .18f));
            Anchor(effect.rectTransform, new Vector2(.18f, .18f), new Vector2(.82f, .82f));
            WDHomeUIFactory.ApplyRounded(effect, 56);
        }
        AddBorder(slot, FigmaCyan, 18);
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
        track.color = Srgb(3, 8, 14, .88f);
        WDHomeUIFactory.ApplyRounded(track, 4);
        Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
        scrollbar.direction = Scrollbar.Direction.BottomToTop;
        scrollbar.transition = Selectable.Transition.None;

        RectTransform slidingArea = WDHomeUIFactory.Rect("SlidingArea", scrollbarRect);
        WDHomeUIFactory.Stretch(slidingArea, 1f, 1f, 1f, 1f);
        RectTransform handle = WDHomeUIFactory.Rect("Handle", slidingArea);
        WDHomeUIFactory.Stretch(handle);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = WithAlpha(FigmaGold, .82f);
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
        string selectedId = customization.Preview.GetValueOrDefault(category, string.Empty);
        WDProfileCosmetic selected = repository.Cosmetics.FirstOrDefault(item => item.Id == selectedId);
        return WDProfileRepository.CategoryLabel(category) + "\n" +
            (selected?.Name ?? "NENHUM ITEM");
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
            Image border = button.transform.Find("ThinBorder")?.GetComponent<Image>();
            if (border != null)
                border.color = isSelected ? FigmaCyan : Srgb(5, 10, 18, .90f);
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

    private void BuildCosmeticCustomization(RectTransform grid,
        WDProfileCosmeticCategory targetCategory)
    {
        // 04.1. Molduras e efeitos consomem o catálogo/propriedade existente.
        // Itens bloqueados encaminham à Loja sem conceder propriedade.
        string section = targetCategory == WDProfileCosmeticCategory.Frame
            ? "MOLDURAS DISPONÍVEIS" : "EFEITOS DISPONÍVEIS";
        AddText(grid, "BasicTitle", section, 14f, FigmaCyan,
            new Vector2(.03f, .91f), new Vector2(.97f, .98f)).fontStyle = FontStyles.Bold;
        string equipped = WDProfileIdentityVisuals.EquippedCosmeticId(
            repository.LocalProfile, targetCategory);
        string selected = customization.Preview.GetValueOrDefault(targetCategory, equipped);
        WDProfileCosmetic[] items = repository.Cosmetics
            .Where(item => item.Category == targetCategory).ToArray();
        for (int i = 0; i < items.Length; i++)
        {
            WDProfileCosmetic item = items[i];
            int column = i % 4;
            int row = i / 4;
            float x = .03f + column * .2425f;
            float top = .89f - row * .40f;
            bool isSelected = selected == item.Id;
            string state = !item.Owned ? "BLOQUEADO" :
                isSelected && equipped == item.Id ? "EQUIPADO" :
                isSelected ? "SELECIONADO" : "DISPONÍVEL";
            WDUIPremiumButton button = AddButton(grid, item.Id,
                item.Name + "\n" + state, () =>
                {
                    if (!item.Owned)
                    {
                        openStoreForCosmetic?.Invoke(item);
                        return;
                    }
                    customization.PreviewItem(item.Id);
                    RenderCustomization();
                }, new Vector2(x, top - .34f), new Vector2(x + .225f, top),
                isSelected, true);
            StyleModeButton(button, isSelected);
            Texture2D thumbnail = WDProfileIdentityVisuals.ResolveCosmeticThumbnail(
                targetCategory, item.Id);
            if (thumbnail != null)
            {
                RawImage artwork = Texture("Artwork", button.transform, thumbnail);
                Anchor(artwork.rectTransform, new Vector2(.08f, .25f), new Vector2(.92f, .93f));
                artwork.transform.SetAsFirstSibling();
            }
            if (!item.Owned)
                AddLockIcon(button.transform, "Lock", new Vector2(.5f, .62f), 28f);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                Anchor(label.rectTransform, new Vector2(.04f, .02f), new Vector2(.96f, .24f));
        }
    }

    private void BuildComingSoon(RectTransform parent, string section)
    {
        AddText(parent, "Section", section, 14f, theme.MasterGold,
            new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.88f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddLockIcon(parent, "Lock", new Vector2(0.5f, 0.645f), 28f);
        AddText(parent, "ComingSoon", "EM BREVE", 22f, theme.MasterGold,
            new Vector2(0.15f, 0.42f), new Vector2(0.85f, 0.56f), TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
        AddText(parent, "Description", "Novos itens de personalização serão disponibilizados em breve.",
            13f, FigmaMuted, new Vector2(0.12f, 0.28f), new Vector2(0.88f, 0.42f), TextAlignmentOptions.Center);
    }

    private void BuildComingSoonStrip(RectTransform parent, string title, float minY, float maxY)
    {
        RectTransform strip = Panel("ComingSoon_" + title, parent,
            new Vector2(0.03f, minY), new Vector2(0.97f, maxY),
            WithAlpha(FigmaGold, .20f), FigmaInset);
        AddLockIcon(strip, "Lock", new Vector2(0.065f, 0.5f), 22f);
        AddText(strip, "Title", title + "  ·  EM BREVE", 12f, theme.MasterGold,
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
        Image focusBorder = field.Find("ThinBorder")?.GetComponent<Image>();
        WDHomeUIFactory.ConfigureEditableInputVisuals(
            nicknameInput, field.GetComponent<Image>(), focusBorder,
            WithAlpha(FigmaGold, .20f), FigmaCyan);
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
            new Vector2(0.016667f, 0.059259f), new Vector2(0.983333f, 0.896296f),
            CanonicalGoldBorder(), null, 16);
        if (!presentation.CanShowMatchHistory)
        {
            VisibleHistoryCount = 0;
            AddPrivateState(body, "HISTÓRICO DE PARTIDAS PRIVADO", "Este jogador optou por não exibir seu histórico de partidas.");
            return;
        }
        // 05.1. O cabeçalho e os filtros permanecem fora da viewport rolável.
        AddText(body, "HistoryTitle", "HISTÓRICO", 18f, theme.MasterGold,
            new Vector2(.013f, .91f), new Vector2(.40f, .975f)).fontStyle = FontStyles.Bold;
        (string, WDProfileMode?)[] filters = { ("TODAS", null), ("BATALHA", WDProfileMode.Battle),
            ("1V1", WDProfileMode.OneVsOne), ("2V2", WDProfileMode.TwoVsTwo), ("3V3", WDProfileMode.ThreeVsThree) };
        for (int i = 0; i < filters.Length; i++)
        {
            var filter = filters[i];
            WDUIPremiumButton filterButton = AddButton(body, $"Filter_{filter.Item1}", filter.Item1, () => { historyFilter = filter.Item2; RenderHistory(presentation); },
                new Vector2(0.013f + i * 0.052f, 0.855f), new Vector2(0.058f + i * 0.052f, 0.895f), historyFilter == filter.Item2, true);
            StyleModeButton(filterButton, historyFilter == filter.Item2);
        }
        AddText(body, "Recent", "ÚLTIMAS 10 PARTIDAS", 12f, FigmaMuted,
            new Vector2(0.76f, 0.91f), new Vector2(0.98f, 0.975f), TextAlignmentOptions.Right).fontStyle = FontStyles.Bold;
        IReadOnlyList<WDProfileMatchRecord> rows = WDProfileRepository.FilterHistory(presentation, historyFilter);
        VisibleHistoryCount = rows.Count;
        if (rows.Count == 0)
        {
            AddText(body, "EmptyHistory", "NENHUMA PARTIDA REGISTRADA", 16f,
                FigmaMuted, new Vector2(.12f, .32f), new Vector2(.88f, .68f),
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            return;
        }
        // 05.2. As partidas compartilham uma única superfície; linhas internas
        // são separadas por regras discretas e rolam dentro da área útil.
        RectTransform list = Panel("HistoryList", body,
            new Vector2(.013f, .03f), new Vector2(.987f, .835f),
            WithAlpha(FigmaCyan, .20f), FigmaSelectedSurface);
        list.gameObject.AddComponent<RectMask2D>();
        ScrollRect historyScroll = list.gameObject.AddComponent<ScrollRect>();
        historyScroll.horizontal = false;
        historyScroll.vertical = true;
        historyScroll.movementType = ScrollRect.MovementType.Clamped;
        RectTransform rowContent = WDHomeUIFactory.Rect("Rows", list);
        rowContent.anchorMin = new Vector2(0f, 1f);
        rowContent.anchorMax = new Vector2(1f, 1f);
        rowContent.pivot = new Vector2(.5f, 1f);
        rowContent.anchoredPosition = Vector2.zero;
        rowContent.sizeDelta = new Vector2(0f, Mathf.Max(696f, rows.Count * 155f));
        historyScroll.viewport = list;
        historyScroll.content = rowContent;
        for (int i = 0; i < rows.Count; i++)
        {
            WDProfileMatchRecord row = rows[i];
            RectTransform match = WDHomeUIFactory.Rect($"Match_{i}", rowContent);
            match.anchorMin = new Vector2(0f, 1f);
            match.anchorMax = new Vector2(1f, 1f);
            match.pivot = new Vector2(.5f, 1f);
            match.anchoredPosition = new Vector2(0f, -i * 155f);
            match.sizeDelta = new Vector2(0f, 155f);
            Color resultColor = row.Result.Contains("VITÓRIA") ? FigmaCyan :
                row.Result.Contains("DERROTA") ? Srgb(255, 51, 77) : theme.MasterGold;
            // 05.3. Resultado usa chip sem transformar a linha inteira em cartão.
            RectTransform resultChip = Panel("ResultChip", match,
                new Vector2(0.012f, 0.58f), new Vector2(0.09f, 0.82f),
                resultColor, row.Result.Contains("DERROTA")
                    ? Srgb(18, 13, 11) : FigmaElevated);
            TextMeshProUGUI result = AddText(resultChip, "Result", row.Result, 12f, resultColor,
                Vector2.zero, Vector2.one, TextAlignmentOptions.Center);
            result.font = theme.MasterGabaritoExtraBold ?? result.font;
            result.fontStyle = FontStyles.Normal;
            AddText(match, "Summary", $"{row.MapName} · {ModeLabel(row.Mode)}" +
                (string.IsNullOrEmpty(row.SubmodeId) ? string.Empty : $" · {row.SubmodeId}") +
                $"\n{row.Participants}\n{row.PlayedAt}",
                13f, FigmaText, new Vector2(0.14f, 0.16f), new Vector2(0.67f, 0.86f));
            AddText(match, "MatchId", $"PARTIDA: {row.MatchId}", 13f, FigmaText,
                new Vector2(0.70f, 0.62f), new Vector2(0.985f, 0.86f), TextAlignmentOptions.Right).fontStyle = FontStyles.Bold;
            WDUIPremiumButton copyMatchId = AddButton(match, $"Copy_{i}", "COPIAR ID",
                () => Copy(row.MatchId), new Vector2(0.82f, 0.42f),
                new Vector2(0.90f, 0.64f), false, true);
            StyleCyanSecondaryButton(copyMatchId);
            AddButton(match, $"Replay_{i}", "VER DETALHES", () => RequestReplay(row.MatchId),
                new Vector2(0.905f, 0.40f), new Vector2(0.985f, 0.66f), true, true);
            if (i < rows.Count - 1)
                AddRule(match, 0f, FigmaGold, .12f);
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
        Clear(chrome);
        WDUniversalHeaderContent header = WDUniversalHeaderFactory.BuildInset(
            chrome, theme, title, () => navigation.TryBack(),
            openNotifications == null ? null : () => openNotifications.Invoke());
        // 06.1. O Header permanece integralmente sob a autoridade universal
        // do Arsenal; Perfil fornece somente título, ações e dados reais.
        WDUniversalHeaderFactory.SetEconomy(header, economyProvider?.Invoke());
    }

    // ============================================================
    // 06.2. SHELL RESPONSIVO COMPARTILHADO PELO HEADER E PELO CONTEÚDO
    // ============================================================
    private void FitSafeArea()
    {
        if (responsiveShell == null) return;
        responsiveFrame ??= WDHomeSecondaryScreenFrame.Configure(responsiveShell);
        responsiveFrame.RefreshNow();
    }

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max) =>
        Panel(name, parent, min, max, WithAlpha(FigmaGold, .20f));

    private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max,
        Color border, Color? fill = null, int radius = 12)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // 07.1. Somente as superfícies estruturais recebem o acabamento externo
        // canônico. Cartões, chips, inputs e estados mantêm sua semântica local.
        bool structural = name is "IdentityPanel" or "Statistics" or "Performance" or
            "CurrentDivision" or "Achievements" or "History";
        if (structural)
        {
            WDUniversalPanelFactory.Apply(rect, theme);
        }
        else
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = fill ?? FigmaPanel;
            image.raycastTarget = true;
            WDHomeUIFactory.ApplyRounded(image, radius);
            AddBorder(rect, border, radius);
        }
        return rect;
    }

    private TextMeshProUGUI AddText(Transform parent, string name, string value, float size, Color color,
        Vector2 min, Vector2 max, TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        TextMeshProUGUI label = WDHomeUIFactory.Text(name, parent, theme, value, size, color, alignment);
        bool displayFace = name.Contains("Title", StringComparison.Ordinal) ||
            name is "Nickname" or "Value" or "DivisionName" or "Result" or "Trophies";
        label.font = displayFace
            ? theme.MasterGabaritoBold ?? label.font
            : theme.MasterInterRegular ?? label.font;
        label.rectTransform.anchorMin = min; label.rectTransform.anchorMax = max;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        if (name == "Nickname" || name == "PlayerId")
            WDUITextPolicies.Apply(label, WDUITextPolicy.SingleLineEllipsis);
        else if (name == "Division" || name == "Equipped" || name == "Preview")
            WDUITextPolicies.Apply(label, WDUITextPolicy.BoundedWrap, 6);
        return label;
    }

    private void AddSectionTitle(Transform parent, string value, Color color)
    {
        TextMeshProUGUI title = AddText(parent, "SectionTitle", value, 18f, color,
            new Vector2(0.02f, 0.73f), new Vector2(0.72f, 0.96f));
        title.font = theme.MasterGabaritoExtraBold ?? title.font;
        title.fontStyle = FontStyles.Normal;
    }

    private WDUIPremiumButton AddButton(Transform parent, string name, string label, UnityAction action,
        Vector2 min, Vector2 max, bool primary = false, bool compact = false)
    {
        RectTransform rect = WDHomeUIFactory.Rect(name, parent); rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        // 07.3. O botão cria uma única borda fina; o Perfil não sobrepõe uma segunda imagem decorativa.
        WDUIPremiumButton button = rect.gameObject.AddComponent<WDUIPremiumButton>();
        button.Build(theme, label, action, true);
        Image image = rect.GetComponent<Image>();
        if (image != null)
        {
            image.color = primary ? theme.MasterGold : theme.MasterSurfaceElevated;
            WDHomeUIFactory.ApplyRounded(image, compact ? 7 : 9);
        }
        TextMeshProUGUI text = rect.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.font = theme.MasterGabaritoBold ?? text.font;
            text.color = primary ? theme.MasterBackground : theme.MasterGold;
            text.fontSize = compact ? 11f : 14f;
            text.fontStyle = FontStyles.Normal;
        }
        if (primary)
        {
            button.SetPersistentColors(theme.MasterGold,
                Color.Lerp(theme.MasterGold, Color.white, 0.12f));
            button.SetLabelColor(theme.MasterBackground);
        }
        Image border = rect.Find("ThinBorder")?.GetComponent<Image>();
        if (border != null)
            border.color = primary ? theme.MasterGold : CanonicalGoldBorder();
        return button;
    }

    // 07.3.1. Ações de cópia usam a variante ciano documentada no Perfil e
    // não herdam o contorno dourado dos demais botões secundários.
    private void StyleCyanSecondaryButton(WDUIPremiumButton button)
    {
        if (button == null) return;
        Image border = button.transform.Find("ThinBorder")?.GetComponent<Image>();
        if (border != null) border.color = WithAlpha(theme.MasterCyan, .24f);
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null) label.color = theme.MasterCyan;
    }

    // 07.3.2. Contornos dourados locais usam exatamente a mesma intensidade
    // fina dos painéis canônicos, sem alterar textos ou estados semânticos.
    private Color CanonicalGoldBorder()
    {
        return WithAlpha(theme.MasterGold, .24f);
    }

    private void StyleCanonicalGoldBorder(WDUIPremiumButton button)
    {
        if (button == null) return;
        Image border = button.transform.Find("ThinBorder")?.GetComponent<Image>();
        if (border != null) border.color = CanonicalGoldBorder();
    }

    private void AddPrivateState(Transform parent, string title, string body)
    {
        bool cyan = parent.name == "Performance";
        AddLockIcon(parent, "PrivateLock", new Vector2(0.5f, 0.66f), 24f,
            cyan ? FigmaCyan : FigmaGold);
        TextMeshProUGUI heading = AddText(parent, "PrivateTitle", title, 18f, FigmaText,
            new Vector2(0.08f, 0.34f), new Vector2(0.92f, 0.53f), TextAlignmentOptions.Center);
        heading.font = theme.MasterGabaritoExtraBold ?? heading.font;
        heading.fontStyle = FontStyles.Normal;
        AddText(parent, "PrivateBody", body, 14f, FigmaMuted,
            new Vector2(0.08f, 0.16f), new Vector2(0.92f, 0.35f), TextAlignmentOptions.Center);
    }

    // ============================================================
    // 07. COMPONENTES VISUAIS DO PERFIL FIGMA FASE 6
    // ============================================================

    private void AddIdentityFact(Transform parent, string name, Texture2D iconTexture,
        string eyebrow, string value, Color accent, float centerY, Color iconColor,
        WDDivisionDefinition division = null)
    {
        RectTransform iconHolder = WDHomeUIFactory.Rect(name + "IconHolder", parent);
        // 07.4. Identidade usa os ícones compactos de 20 px do frame 355:9.
        iconHolder.anchorMin = iconHolder.anchorMax = new Vector2(0.09f, centerY);
        iconHolder.pivot = new Vector2(0.5f, 0.5f);
        iconHolder.anchoredPosition = new Vector2(0f, -3f);
        const float iconSide = 20f;
        iconHolder.sizeDelta = new Vector2(iconSide, iconSide);
        // 07.4. O item de divisão usa o mesmo medalhão-base reaproveitado pelas
        // onze divisões; o emblema específico permanece como camada interna.
        if (division != null)
        {
            RawImage medallion = Texture(name + "Medallion", iconHolder,
                WDDivisionVisuals.ResolveMedallion());
            WDHomeUIFactory.Stretch(medallion.rectTransform);
        }
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
        const float textLeft = 0.15f;
        TextMeshProUGUI eyebrowLabel = AddText(parent, name + "Eyebrow", eyebrow, 11f, FigmaMuted,
            new Vector2(textLeft, centerY), new Vector2(0.92f, centerY + 0.028f));
        eyebrowLabel.font = theme.MasterInterSemiBold ?? eyebrowLabel.font;
        TextMeshProUGUI label = AddText(parent, name, value, 15f, accent,
            new Vector2(textLeft, centerY - 0.028f), new Vector2(0.92f, centerY));
        label.font = theme.MasterGabaritoBold ?? label.font;
        label.fontStyle = FontStyles.Normal;
    }

    private void AddFrameSummary(Transform parent, WDProfileData profile, float minY, float maxY)
    {
        string frameId = WDProfileIdentityVisuals.EquippedCosmeticId(
            profile, WDProfileCosmeticCategory.Frame);
        WDProfileCosmetic frame = repository.Cosmetics.FirstOrDefault(item =>
            item.Category == WDProfileCosmeticCategory.Frame && item.Id == frameId);
        RectTransform frameTile = Panel("FramePreview", parent,
            new Vector2(0.02f, minY + 0.05f), new Vector2(0.24f, maxY - 0.05f),
            CanonicalGoldBorder(), FigmaInset, 6);
        Texture2D thumbnail = WDProfileIdentityVisuals.ResolveCosmeticThumbnail(
            WDProfileCosmeticCategory.Frame, frameId);
        if (thumbnail != null)
        {
            RawImage artwork = Texture("Artwork", frameTile, thumbnail);
            WDHomeUIFactory.Stretch(artwork.rectTransform, 4f, 4f, 4f, 4f);
        }
        // 07.2. O tile já recebe sua borda pelo Panel; não sobrepor uma segunda moldura quando não há thumbnail.
        AddText(parent, "FrameTitle", "MOLDURA", 13f, FigmaText,
            new Vector2(0.28f, minY + 0.19f), new Vector2(0.96f, maxY - 0.06f)).fontStyle = FontStyles.Bold;
        AddText(parent, "FrameState", frame?.Name ?? "Nenhuma moldura", 11f, FigmaMuted,
            new Vector2(0.28f, minY + 0.02f), new Vector2(0.96f, minY + 0.20f));
    }

    private void AddSkinSummary(Transform parent, WDProfileData profile, float minY, float maxY)
    {
        string skinId = WDProfileIdentityVisuals.EquippedCosmeticId(
            profile, WDProfileCosmeticCategory.TerritorySkin);
        WDProfileSkinDefinition skin = WDProfileSkinCatalog.Find(skinId) ?? WDProfileSkinCatalog.Default;
        // 07.5. A miniatura canônica tem prioridade; a cor permanece fallback de dados.
        Texture2D thumbnail = WDProfileIdentityVisuals.ResolveCosmeticThumbnail(
            WDProfileCosmeticCategory.TerritorySkin, skinId);
        RectTransform skinTile = WDHomeUIFactory.Rect("SkinPreview", parent);
        Anchor(skinTile, new Vector2(0.02f, minY + 0.05f), new Vector2(0.24f, maxY - 0.05f));
        Image swatch = skinTile.gameObject.AddComponent<Image>();
        swatch.color = thumbnail == null ? skin.Color : Color.white;
        WDHomeUIFactory.ApplyRounded(swatch, 6);
        if (thumbnail != null)
        {
            RawImage artwork = Texture("Artwork", skinTile, thumbnail);
            WDHomeUIFactory.Stretch(artwork.rectTransform, 2f, 2f, 2f, 2f);
        }
        AddBorder(skinTile, WithAlpha(FigmaGold, .20f), 6);
        AddText(parent, "SkinTitle", "SKIN DE TERRITÓRIO", 13f, FigmaText,
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
            ("TAXA DE VITÓRIA", stats.WinRate.ToString("0.0") + "%", theme.MasterGold) };
        float gap = 0.012f;
        float width = (max.x - min.x - gap * 3f) / 4f;
        for (int i = 0; i < metrics.Length; i++)
        {
            float x = min.x + i * (width + gap);
            RectTransform box = Panel("Metric_" + i, parent,
                new Vector2(x, min.y), new Vector2(x + width, max.y),
                cyanBorders
                    ? WithAlpha(FigmaCyan, .20f)
                    : CanonicalGoldBorder(),
                cyanBorders ? theme.MasterCyanSoft : theme.MasterSurfacePrimary, 8);
            TextMeshProUGUI metricLabel = AddText(box, "Label", metrics[i].label, large ? 12f : 10f, FigmaMuted,
                new Vector2(0.06f, 0.55f), new Vector2(0.94f, 0.90f));
            metricLabel.font = theme.MasterInterSemiBold ?? metricLabel.font;
            metricLabel.fontStyle = FontStyles.Normal;
            TextMeshProUGUI metricValue = AddText(box, "Value", metrics[i].value, large ? 28f : 18f, metrics[i].color,
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.58f));
            metricValue.font = theme.MasterGabaritoExtraBold ?? metricValue.font;
            metricValue.fontStyle = FontStyles.Normal;
        }
    }

    private void BuildDivisionDisplay(RectTransform parent, WDDivisionDefinition division, int points)
    {
        RectTransform medal = WDHomeUIFactory.Rect("DivisionMedallion", parent);
        // 07.6. O widget aprovado utiliza o medalhão canônico em 64 px.
        medal.anchorMin = medal.anchorMax = new Vector2(0.15f, 0.40f);
        medal.pivot = new Vector2(0.5f, 0.5f);
        medal.anchoredPosition = Vector2.zero;
        medal.sizeDelta = new Vector2(64f, 64f);
        RawImage gold = Texture("Gold", medal, WDDivisionVisuals.ResolveMedallion());
        WDHomeUIFactory.Stretch(gold.rectTransform);
        Texture2D emblemTexture = ResolveDivisionEmblemTexture(division);
        RawImage emblem = Texture("Emblem", medal, emblemTexture);
        Anchor(emblem.rectTransform, new Vector2(0.07f, 0.07f), new Vector2(0.93f, 0.93f));
        WDDivisionVisuals.ApplyCalibration(emblem.rectTransform, division, 64f);
        AddText(parent, "DivisionName", division?.DisplayName ?? string.Empty, 20f, theme.MasterGold,
            new Vector2(0.27f, 0.39f), new Vector2(0.96f, 0.60f)).fontStyle = FontStyles.Bold;
        AddText(parent, "DivisionPoints", $"{points:N0} PONTOS", 14f, FigmaText,
            new Vector2(0.27f, 0.21f), new Vector2(0.96f, 0.40f));
    }

    private void BuildAchievements(RectTransform parent, IReadOnlyList<WDProfileAchievement> achievements)
    {
        if (achievements == null || achievements.Count == 0)
        {
            AddText(parent, "EmptyAchievements", "NENHUMA CONQUISTA REGISTRADA", 13f,
                FigmaMuted, new Vector2(.08f, .12f), new Vector2(.92f, .66f),
                TextAlignmentOptions.Center).fontStyle = FontStyles.Bold;
            return;
        }
        int count = Mathf.Min(4, achievements.Count);
        for (int i = 0; i < count; i++)
        {
            WDProfileAchievement item = achievements[i];
            float minX = 0.02f + i * 0.245f;
            RectTransform card = Panel("Achievement_" + i, parent,
                new Vector2(minX, 0.08f), new Vector2(minX + 0.23f, 0.67f),
                WithAlpha(FigmaCyan, .20f), FigmaInsetSoft, 8);
            // 07.7. O selo circular de 48 px separa o ícone do fundo do cartão.
            RectTransform badge = WDHomeUIFactory.Rect("Badge", card);
            badge.anchorMin = badge.anchorMax = new Vector2(.12f, .5f);
            badge.pivot = new Vector2(.5f, .5f);
            badge.sizeDelta = new Vector2(48f, 48f);
            Image badgeSurface = badge.gameObject.AddComponent<Image>();
            badgeSurface.color = FigmaElevated;
            WDHomeUIFactory.ApplyRounded(badgeSurface, 24);
            AddBorder(badge, FigmaCyan, 24);
            RawImage icon = Texture("Icon", badge,
                WDProfileIdentityVisuals.ResolveAchievement(item.Id));
            icon.color = FigmaCyan;
            Anchor(icon.rectTransform, new Vector2(.25f, .25f), new Vector2(.75f, .75f));
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

    // 07.8. Conversão centralizada dos tokens sRGB da especificação para o
    // espaço linear usado pelo projeto; alpha permanece uma cobertura linear.
    private static Color Srgb(byte red, byte green, byte blue, float alpha = 1f)
    {
        Color color = new(red / 255f, green / 255f, blue / 255f, alpha);
        Color linear = color.linear;
        linear.a = alpha;
        return linear;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static void ApplyProfileSurface(RectTransform root, Color fill,
        Color borderColor, int radius)
    {
        if (root == null) return;
        Image surface = root.GetComponent<Image>();
        if (surface != null)
        {
            surface.color = fill;
            WDHomeUIFactory.ApplyRounded(surface, radius);
        }
        // 07.9. Substitui a moldura herdada na própria instância para manter
        // exatamente um traço e impedir sobreposição de Border/ThinBorder.
        Transform inheritedBorder = root.Find("Border");
        if (inheritedBorder != null) DestroyNow(inheritedBorder.gameObject);
        Transform inheritedThinBorder = root.Find("ThinBorder");
        if (inheritedThinBorder != null) DestroyNow(inheritedThinBorder.gameObject);
        WDHomeUIFactory.AddThinRoundedBorder(root, borderColor, radius);
    }

    private static void AddBorder(RectTransform target, Color color, int radius) =>
        WDHomeUIFactory.AddThinRoundedBorder(target, color, radius);

    private static void AddLockIcon(Transform parent, string name, Vector2 anchor, float side,
        Color? tint = null)
    {
        RectTransform holder = WDHomeUIFactory.Rect(name, parent);
        holder.anchorMin = holder.anchorMax = anchor;
        holder.pivot = new Vector2(0.5f, 0.5f);
        holder.anchoredPosition = Vector2.zero;
        holder.sizeDelta = new Vector2(side, side);
        RawImage icon = holder.gameObject.AddComponent<RawImage>();
        icon.texture = WDProfileIdentityVisuals.ResolveLockIcon();
        icon.color = tint ?? FigmaMuted;
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
                new Vector2(0.025f + i * 0.105f, 0.51f), new Vector2(0.12f + i * 0.105f, 0.69f),
                selectedMode == captured, true);
            StyleModeButton(tab, selectedMode == captured);
        }
        WDProfileStats selected = modeStats.TryGetValue(selectedMode, out WDProfileStats value)
            ? value : new WDProfileStats(0, 0);
        BuildModeMetricStrip(parent, selected);
    }

    private void BuildModeMetricStrip(Transform parent, WDProfileStats stats)
    {
        // 08.1. Uma única superfície agrupa as quatro métricas; regras internas
        // substituem os quatro cartões concorrentes da implementação anterior.
        RectTransform strip = Panel("SelectedModeMetrics", parent,
            new Vector2(.025f, .08f), new Vector2(.975f, .43f),
            WithAlpha(FigmaCyan, .20f),
            FigmaSelectedSurface, 8);
        (string label, string value, Color color)[] metrics = {
            ("PARTIDAS", stats.Matches.ToString("N0"), FigmaText),
            ("VITÓRIAS", stats.Wins.ToString("N0"), FigmaCyan),
            ("DERROTAS", stats.Losses.ToString("N0"), FigmaText),
            ("TAXA DE VITÓRIA", stats.WinRate.ToString("0.0") + "%", theme.MasterGold) };
        for (int i = 0; i < metrics.Length; i++)
        {
            float minX = i * .25f + .018f;
            float maxX = (i + 1) * .25f - .018f;
            AddText(strip, "ModeLabel_" + i, metrics[i].label, 10f, FigmaMuted,
                new Vector2(minX, .54f), new Vector2(maxX, .86f)).fontStyle = FontStyles.Bold;
            AddText(strip, "ModeValue_" + i, metrics[i].value, 18f, metrics[i].color,
                new Vector2(minX, .12f), new Vector2(maxX, .55f)).fontStyle = FontStyles.Bold;
            if (i < metrics.Length - 1)
            {
                Image divider = WDHomeUIFactory.Image("Divider_" + i, strip,
                    WithAlpha(FigmaGold, .12f));
                Anchor(divider.rectTransform, new Vector2((i + 1) * .25f, .18f),
                    new Vector2((i + 1) * .25f + .001f, .82f));
            }
        }
    }

    private void StyleModeButton(WDUIPremiumButton button, bool selected)
    {
        if (button == null) return;
        Image surface = button.GetComponent<Image>();
        if (surface != null)
            surface.color = selected
                ? theme.MasterCyanSoft
                : theme.MasterSurfaceElevated;
        Image border = button.transform.Find("ThinBorder")?.GetComponent<Image>();
        if (border != null)
            border.color = selected
                ? theme.MasterCyan
                : CanonicalGoldBorder();
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (label != null)
            label.color = selected ? theme.MasterCyan : theme.MasterTextSecondary;
    }

    private GameObject ModalBlocker(string name, UnityAction outsideAction)
    {
        RectTransform root = WDHomeUIFactory.Rect(name, transform);
        WDHomeUIFactory.Stretch(root);
        RectTransform outside = WDHomeUIFactory.Rect("OutsideDismiss", root);
        WDHomeUIFactory.Stretch(outside);
        Image image = outside.gameObject.AddComponent<Image>();
        image.color = FigmaModalBackdrop;
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
