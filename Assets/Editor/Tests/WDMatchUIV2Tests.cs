#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class WDMatchUIV2Tests
{
    [Test]
    public void RadarModalUsesBlockingBackdropAndExplicitConfirmationChrome()
    {
        var root = new GameObject("RadarModalFixture", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            root.AddComponent<WDMatchUIV2>().Build(theme, null, null, null);
            Transform modal = root.transform.Find("RadarModal");
            Transform panel = modal.Find("Panel");

            Assert.That(modal.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(modal.GetComponent<Button>(), Is.Not.Null);
            Assert.That(panel.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(panel.GetComponent<EventTrigger>(), Is.Not.Null);
            Assert.That(panel.Find("Cancel"), Is.Not.Null);
            Assert.That(panel.Find("Confirm"), Is.Not.Null);
            Assert.That(modal.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(theme);
        }
    }

    // 00. Contraste acompanha entries renderizadas, sem mudar layout/autoridade de jogadores.
    [TestCase(1920f)]
    [TestCase(2400f)]
    public void PlayersContrastFollowsEntriesBehindListInsideSafeArea(float width)
    {
        var root = new GameObject("SafeArea", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            var area = (RectTransform)root.transform;
            area.sizeDelta = new Vector2(width, 1080);
            // A composição real coloca V2 como filha da Safe Area. Build estica apenas
            // essa filha; aplicá-lo na raiz apagaria o tamanho da área usada na assertion.
            var viewRect = WDUIFactory.Rect("MatchUIV2", area);
            var view = viewRect.gameObject.AddComponent<WDMatchUIV2>();
            view.Build(theme, null, null, null);
            Assert.That(area.rect.size, Is.EqualTo(new Vector2(width, 1080)));
            Assert.That(viewRect.rect.size, Is.EqualTo(area.rect.size));
            var entries = (RectTransform)viewRect.Find("Players");
            var contrast = viewRect.Find("PlayersContrast").GetComponent<WDMatchPlayersContrast>();
            var panel = (RectTransform)contrast.transform;
            Assert.That(panel.GetSiblingIndex(), Is.LessThan(entries.GetSiblingIndex()));
            Assert.That(panel.GetComponent<Selectable>(), Is.Null);
            Assert.That(panel.GetComponent<EventTrigger>(), Is.Null);
            foreach (var graphic in panel.GetComponentsInChildren<Graphic>())
                Assert.That(graphic.raycastTarget, Is.False);
            Assert.That(panel.GetComponent<Image>().enabled, Is.False);
            for (int count = 1; count <= 8; count++)
            {
                var row = WDUIFactory.Rect("PlayerEntry", entries);
                row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
                LayoutRebuilder.ForceRebuildLayoutImmediate(entries);
                contrast.Refresh();
                Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(234, 54 * count + 22 * (count - 1) + 16)));
                Assert.That(entries.anchoredPosition, Is.EqualTo(new Vector2(-28, -334)));
                // O padding é externo e aparece uma única vez em cada lado da lista.
                var entryCorners = new Vector3[4]; entries.GetWorldCorners(entryCorners);
                var panelCorners = new Vector3[4]; panel.GetWorldCorners(panelCorners);
                Vector3 entryMin = area.InverseTransformPoint(entryCorners[0]);
                Vector3 entryMax = area.InverseTransformPoint(entryCorners[2]);
                Vector3 panelMin = area.InverseTransformPoint(panelCorners[0]);
                Vector3 panelMax = area.InverseTransformPoint(panelCorners[2]);
                Assert.That(area.rect.xMax - panelMax.x, Is.EqualTo(20).Within(.001f),
                    "Margem externa aprovada, medida a partir da borda direita da Safe Area.");
                Assert.That(entryMin.x - panelMin.x, Is.EqualTo(8).Within(.001f));
                Assert.That(entryMin.y - panelMin.y, Is.EqualTo(8).Within(.001f));
                Assert.That(panelMax.x - entryMax.x, Is.EqualTo(8).Within(.001f));
                Assert.That(panelMax.y - entryMax.y, Is.EqualTo(8).Within(.001f));
                var corners = new Vector3[4]; panel.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 p = area.InverseTransformPoint(corner);
                    Assert.That(Mathf.Abs(p.x), Is.LessThanOrEqualTo(width / 2));
                    Assert.That(Mathf.Abs(p.y), Is.LessThanOrEqualTo(540));
                }
            }
            // Entry não renderizada não reserva espaço; remover participantes reduz o painel.
            entries.GetChild(7).gameObject.SetActive(false);
            contrast.Refresh();
            Assert.That(panel.sizeDelta.y, Is.EqualTo(7 * 54 + 6 * 22 + 16));
            for (int i = entries.childCount - 1; i >= 0; i--) Object.DestroyImmediate(entries.GetChild(i).gameObject);
            contrast.Refresh();
            Assert.That(panel.GetComponent<Image>().enabled, Is.False);
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(theme); }
    }

    [Test]
    public void MenuAndChatShareReadableBodyWithoutChangingSettingsOrBackdrops()
    {
        var root = new GameObject("ContrastTest", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            root.AddComponent<WDMatchUIV2>().Build(theme, null, null, null);
            foreach (string path in new[] { "MenuPlayersModal/Panel", "MatchChat/Panel", "PlayersContrast" })
                Assert.That(root.transform.Find(path).GetComponent<Image>().color,
                    Is.EqualTo(WDMatchVisualStyle.SurfaceMatchReadable), path);
            Assert.That(WDMatchVisualStyle.SurfaceMatchReadable.a, Is.EqualTo(.95f));
            Assert.That(root.transform.Find("MatchChat/Backdrop").GetComponent<Image>().color.a, Is.EqualTo(.55f));
            Assert.That(root.transform.Find("MatchSettings/SettingsPanel").GetComponent<Image>().color.a, Is.EqualTo(1f));
            Assert.That(root.transform.Find("MatchSettings/LeaveConfirmation").GetComponent<Image>().color.a, Is.EqualTo(1f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(theme); }
    }

    // ============================================================
    // 01. INVARIANTES DE FASE DA APRESENTAÇÃO V2
    // ============================================================

    [Test]
    public void ExistingHudSurfacesStayReadableIncludingDisabledButtonsWithoutResizing()
    {
        var root = new GameObject("HudSurfaceTest", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            root.AddComponent<WDMatchUIV2>().Build(theme, null, null, null);
            string[] paths = { "Resources", "UtilityControls", "LeftCommandRail", "SendMoves", "MovementContext" };
            Vector2[] sizes = { new(240, 60), new(118, 48), new(273, 874), new(230, 69), new(650, 88) };
            Vector2[] positions = { new(69, -12), new(-54, -24), new(12, -185), new(-20, 58), new(0, 24) };
            for (int i = 0; i < paths.Length; i++)
            {
                var rect = (RectTransform)root.transform.Find(paths[i]);
                Assert.That(rect.GetComponent<Image>().color.a, Is.EqualTo(.95f), paths[i]);
                Assert.That(rect.sizeDelta, Is.EqualTo(sizes[i]), paths[i]);
                Assert.That(rect.anchoredPosition, Is.EqualTo(positions[i]), paths[i]);
            }
            for (int i = 1; i <= 8; i++)
            {
                var card = root.transform.Find("LeftCommandRail/WeaponGrid/WeaponSlot" + i);
                var button = card.GetComponent<WDUIPremiumButton>();
                button.SetInteractable(false);
                Assert.That(card.GetComponent<Image>().color.a, Is.EqualTo(.95f));
                button.SetInteractable(true);
                Assert.That(card.GetComponent<Image>().color.a, Is.EqualTo(.95f));
            }
            var send = root.transform.Find("SendMoves").GetComponent<WDUIPremiumButton>();
            send.SetInteractable(false);
            Assert.That(send.GetComponent<Image>().color.a, Is.EqualTo(.95f));
            send.SetInteractable(true);
            Assert.That(send.GetComponent<Image>().color.a, Is.EqualTo(.95f));
        }
        finally { Object.DestroyImmediate(root); Object.DestroyImmediate(theme); }
    }

    [Test]
    public void SendMovesExistsOnlyDuringPreparation()
    {
        Assert.That(WDMatchUIV2.ShouldShowSendMoves(
            GameManager.FaseTurno.Preparacao), Is.True);
        Assert.That(WDMatchUIV2.ShouldShowSendMoves(
            GameManager.FaseTurno.Resolucao), Is.False);
    }

    [Test]
    public void ResolutionSpeedPreferenceIsAvailableInBothRoundPhases()
    {
        Assert.That(WDMatchUIV2.ShouldShowResolutionSpeed(
            GameManager.FaseTurno.Preparacao), Is.True);
        Assert.That(WDMatchUIV2.ShouldShowResolutionSpeed(
            GameManager.FaseTurno.Resolucao), Is.True);
    }

    [Test]
    public void MovementAvailabilityReflectsThePublishedLimit()
    {
        Assert.That(WDMatchUIV2.FormatMovementAvailability(1, 3),
            Is.EqualTo("MOVIMENTOS  1/3 · 2 DISP."));
        Assert.That(WDMatchUIV2.FormatMovementAvailability(2, 5),
            Is.EqualTo("MOVIMENTOS  2/5 · 3 DISP."));
    }

    [Test]
    public void ContextSlotShowsTroopsUntilTheyReachZeroThenRemainingMoves()
    {
        Assert.That(WDMatchUIV2.ShouldShowTroopsResource(2), Is.True);
        Assert.That(WDMatchUIV2.ContextualResourceIconPath(2),
            Is.EqualTo("UI/Match/Official/Troops"));
        Assert.That(WDMatchUIV2.FormatContextualResource(2, 0, 3), Is.EqualTo("2"));
        Assert.That(WDMatchUIV2.ShouldShowTroopsResource(0), Is.False);
        Assert.That(WDMatchUIV2.ContextualResourceIconPath(0),
            Is.EqualTo("UI/Match/Official/Movements"));
        Assert.That(WDMatchUIV2.FormatContextualResource(0, 0, 3), Is.EqualTo("3"));
        Assert.That(WDMatchUIV2.FormatContextualResource(0, 1, 3), Is.EqualTo("2"));
        Assert.That(WDMatchUIV2.FormatContextualResource(0, 4, 4), Is.EqualTo("0"));
    }

    [Test]
    public void TimerUsesWholeRemainingSecondsWithoutMinuteFormatting()
    {
        Assert.That(WDMatchUIV2.FormatTimerSeconds(90f), Is.EqualTo("90"));
        Assert.That(WDMatchUIV2.FormatTimerSeconds(89.01f), Is.EqualTo("90"));
        Assert.That(WDMatchUIV2.FormatTimerSeconds(89f), Is.EqualTo("89"));
        Assert.That(WDMatchUIV2.FormatTimerSeconds(.01f), Is.EqualTo("1"));
        Assert.That(WDMatchUIV2.FormatTimerSeconds(0f), Is.EqualTo("0"));
        StringAssert.DoesNotContain(":", WDMatchUIV2.FormatTimerSeconds(90f));
    }

    [Test]
    public void FastForwardUsesIndependentOfficialSpritesForOffAndOn()
    {
        Assert.That(WDMatchUIV2.SpeedIconPath(false),
            Is.EqualTo("UI/Match/Official/SpeedOff"));
        Assert.That(WDMatchUIV2.SpeedIconPath(true),
            Is.EqualTo("UI/Match/Official/SpeedOn"));
    }

    [Test]
    public void ContextSlotUsesTheGameplayPublishedBlitzkriegLimit()
    {
        Assert.That(WDMatchUIV2.RemainingMovements(0,
            WDTacticalWeaponTables.BlitzkriegLimit(1)), Is.EqualTo(4));
        Assert.That(WDMatchUIV2.RemainingMovements(2,
            WDTacticalWeaponTables.BlitzkriegLimit(4)), Is.EqualTo(5));
    }

    [Test]
    public void WeaponCardUsesTheEnergyGlyphInsteadOfTemporaryELabel()
    {
        string label = WDMatchUIV2.FormatWeaponCardLabel("DRONE", 1, 30);
        StringAssert.Contains("⚡ 30", label);
        StringAssert.DoesNotContain("· E 30", label);
    }

    [Test]
    public void BaseAndBlitzkriegLimitsRemainGameplayOwned()
    {
        Assert.That(FilaAcoes.MaximoAcoesPorRodada, Is.EqualTo(3));
        Assert.That(WDTacticalWeaponTables.BlitzkriegLimit(1), Is.EqualTo(4));
        Assert.That(WDTacticalWeaponTables.BlitzkriegLimit(2), Is.EqualTo(5));
        Assert.That(WDTacticalWeaponTables.BlitzkriegLimit(3), Is.EqualTo(6));
    }

    [Test]
    public void ResolutionSequenceOwnsAndPreservesSpeedPreference()
    {
        var root = new GameObject("ResolutionSpeedPreferenceTest");
        try
        {
            ResolutionSequenceController sequence =
                root.AddComponent<ResolutionSequenceController>();
            Assert.That(sequence.IsAccelerated, Is.False);
            sequence.TogglePlaybackSpeed();
            Assert.That(sequence.IsAccelerated, Is.True);
            Assert.That(sequence.PlaybackSpeed, Is.EqualTo(2f));
            sequence.TogglePlaybackSpeed();
            Assert.That(sequence.PlaybackSpeed, Is.EqualTo(1f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ResolutionPresentationConsumesPlaybackSpeed()
    {
        Assert.That(ResolutionVisualPresentationContext.ScaleDeltaTime(.25f, 1f),
            Is.EqualTo(.25f));
        Assert.That(ResolutionVisualPresentationContext.ScaleDeltaTime(.25f, 2f),
            Is.EqualTo(.5f));
    }

    [Test]
    public void DeckOneNeverBecomesAVisibleV2Label()
    {
        Assert.That(WDMatchUIV2.ContainsDeckOneLabel("ROUND 1"), Is.False);
        Assert.That(WDMatchUIV2.ContainsDeckOneLabel("DECK 1"), Is.True);
    }

    // ============================================================
    // 02. AUTORIDADE ÚNICA DO PRESENTATION BINDING
    // ============================================================

    [Test]
    public void RegisterReplacesThePreviousPresentationAuthority()
    {
        var root = new GameObject("PresentationBindingTest");
        try
        {
            WDMatchUIPresentationBinding binding =
                root.AddComponent<WDMatchUIPresentationBinding>();
            var first = new PresentationSpy();
            var second = new PresentationSpy();
            binding.Register(first);
            binding.Register(second);
            binding.Present(null);
            Assert.That(first.PresentCalls, Is.Zero);
            Assert.That(second.PresentCalls, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void BindingPropagatesTheSingleUtilityAuthorityToTheV2Presentation()
    {
        var root = new GameObject("UtilityBindingTest");
        try
        {
            WDMatchUIPresentationBinding binding =
                root.AddComponent<WDMatchUIPresentationBinding>();
            var utilities = new WDMatchUtilityCommands(null, () => false);
            var presentation = new PresentationSpy();

            binding.Configure(null, null, null, utilities);
            binding.Register(presentation);

            Assert.That(binding.Utilities, Is.SameAs(utilities));
            Assert.That(presentation.Utilities, Is.SameAs(utilities));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void UtilityCoordinatorTogglesRegionsAndClosesThemBeforeAnotherRoute()
    {
        bool regionsOpen = false;
        var utilities = new WDMatchUtilityCommands(
            () => regionsOpen = !regionsOpen,
            () => regionsOpen);
        WDMatchUtilityModule requested = WDMatchUtilityModule.None;
        utilities.ModuleRequested += module => requested = module;

        utilities.Toggle(WDMatchUtilityModule.Regions);
        Assert.That(regionsOpen, Is.True);
        Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Regions));
        Assert.That(requested, Is.EqualTo(WDMatchUtilityModule.Regions));

        utilities.Toggle(WDMatchUtilityModule.Chat);
        Assert.That(regionsOpen, Is.False);
        Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Chat));
        Assert.That(requested, Is.EqualTo(WDMatchUtilityModule.Chat));
    }

    [Test]
    public void UtilityCoordinatorProvidesRoutesForMenuChatAndSettings()
    {
        var utilities = new WDMatchUtilityCommands(null, () => false);
        WDMatchUtilityModule requested = WDMatchUtilityModule.None;
        utilities.ModuleRequested += module => requested = module;

        foreach (WDMatchUtilityModule module in new[]
                 {
                     WDMatchUtilityModule.Menu,
                     WDMatchUtilityModule.Chat,
                     WDMatchUtilityModule.Settings
                 })
        {
            utilities.Toggle(module);
            Assert.That(utilities.ActiveModule, Is.EqualTo(module));
            Assert.That(requested, Is.EqualTo(module));
            utilities.Toggle(module);
            Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
        }
    }

    [Test]
    public void MenuButtonHasOneFunctionalCallbackAndTogglesTheCanonicalModal()
    {
        var root = new GameObject("MatchUIV2MenuTest", typeof(RectTransform));
        WarDominionUITheme theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            WDMatchUIV2 view = root.AddComponent<WDMatchUIV2>();
            view.Build(theme, null, null, null);
            var utilities = new WDMatchUtilityCommands(null, () => false);
            view.BindUtilities(utilities);
            Button menu = root.transform.Find(
                "LeftCommandRail/PrimaryNavigation/Menu")?.GetComponent<Button>();
            Transform modal = root.transform.Find("MenuPlayersModal");

            Assert.That(menu, Is.Not.Null);
            Assert.That(modal, Is.Not.Null);
            Assert.That(modal.gameObject.activeSelf, Is.False);
            menu.onClick.Invoke();
            Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Menu));
            Assert.That(modal.gameObject.activeSelf, Is.True,
                "Um callback duplicado fecharia o modal no mesmo clique.");
            menu.onClick.Invoke();
            Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
            Assert.That(modal.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void BackdropClosesMenuButPanelConsumesInternalClicks()
    {
        var root = new GameObject("MatchUIV2BackdropTest", typeof(RectTransform));
        var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
        WarDominionUITheme theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            WDMatchUIV2 view = root.AddComponent<WDMatchUIV2>();
            view.Build(theme, null, null, null);
            var utilities = new WDMatchUtilityCommands(null, () => false);
            view.BindUtilities(utilities);
            utilities.Toggle(WDMatchUtilityModule.Menu);

            Transform modal = root.transform.Find("MenuPlayersModal");
            Transform panel = modal.Find("Panel");
            Assert.That(modal.GetComponent<Button>(), Is.Not.Null);
            Assert.That(panel.GetComponent<EventTrigger>(), Is.Not.Null);

            ExecuteEvents.Execute(panel.gameObject,
                new PointerEventData(eventSystemObject.GetComponent<EventSystem>()),
                ExecuteEvents.pointerClickHandler);
            Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Menu));
            Assert.That(modal.gameObject.activeSelf, Is.True);

            modal.GetComponent<Button>().onClick.Invoke();
            Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.None));
            Assert.That(modal.gameObject.activeSelf, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(eventSystemObject);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void MenuAndRegionsAreExclusiveInBothDirections()
    {
        bool regionsOpen = false;
        var utilities = new WDMatchUtilityCommands(
            () => regionsOpen = !regionsOpen,
            () => regionsOpen);

        utilities.Toggle(WDMatchUtilityModule.Menu);
        utilities.Toggle(WDMatchUtilityModule.Regions);
        Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Regions));
        Assert.That(regionsOpen, Is.True);

        utilities.Toggle(WDMatchUtilityModule.Menu);
        Assert.That(regionsOpen, Is.False);
        Assert.That(utilities.ActiveModule, Is.EqualTo(WDMatchUtilityModule.Menu));
    }

    [Test]
    public void MenuTitleAndPlayerIdentityAreRuntimeFormatted()
    {
        Assert.That(WDMatchUIV2.FormatMenuTitle(7),
            Is.EqualTo("JOGADORES · ROUND 7"));
        Assert.That(WDMatchUIV2.FormatMenuPlayerName("Comandante Aurora"),
            Is.EqualTo("COMANDANTE AURORA"));
    }

    [Test]
    public void MenuUsesThreeDistinctOfficialIconAssetsWithoutTextLabels()
    {
        Assert.That(WDMatchUIV2.PlayerStatisticIconPaths, Is.EqualTo(new[]
        {
            "UI/Match/Official/PlayerStatTerritories",
            "UI/Match/Official/PlayerStatTroops",
            "UI/Match/Official/PlayerStatEnergy"
        }));
        Assert.That(WDMatchUIV2.PlayerStatisticIconPaths,
            Has.All.Not.Contains("Label"));
    }

    [Test]
    public void MenuReservesLongNameWidthBeforeFixedStatsAndSlots()
    {
        Assert.That(WDMatchUIV2.MenuPlayerNameBounds,
            Is.EqualTo(new Vector2(126f, 220f)));
        Assert.That(WDMatchUIV2.MenuPlayerHorizontalLayout,
            Is.EqualTo(new Vector4(360f, 400f, 440f, 500f)));
        Assert.That(WDMatchUIV2.MenuPlayerNameBounds.x +
            WDMatchUIV2.MenuPlayerNameBounds.y,
            Is.LessThan(WDMatchUIV2.MenuPlayerHorizontalLayout.x));
    }

    [Test]
    public void RegionPresentationReadsDynamicMapDataAndUsesFigmaClassicPalette()
    {
        var region = new DefinicaoRegiaoMapa
        {
            id = "europa",
            nomeExibido = "Europa Teste",
            bonus = 9,
            corDestaque = Color.magenta
        };

        Assert.That(WDMatchInformationalOverlayController.DescribeRegion(region),
            Is.EqualTo("Europa Teste\nBÔNUS +9"));
        Assert.That(WDMatchInformationalOverlayController.DescribeRegionBonus(region),
            Is.EqualTo("+9"));
        Assert.That(WDMatchInformationalOverlayController
            .ResolveRegionPresentationColor(region),
            Is.EqualTo(new Color(146f / 255f, 112f / 255f, 200f / 255f)));

        region.id = "mapa_customizado";
        Assert.That(WDMatchInformationalOverlayController
            .ResolveRegionPresentationColor(region), Is.EqualTo(Color.magenta));
    }

    [Test]
    public void WeaponPresentationRebindsToTheCurrentControllerWithoutAnotherAuthority()
    {
        var root = new GameObject("WeaponBinding");
        var other = new GameObject("ReplacementController");
        try
        {
            var binding = root.AddComponent<WDMatchUIPresentationBinding>();
            var first = root.AddComponent<WDWeaponMatchUIController>();
            var second = other.AddComponent<WDWeaponMatchUIController>();
            var spy = new PresentationSpy();
            binding.Configure(null, first, null);
            binding.Register(spy);
            Assert.That(spy.Weapons, Is.SameAs(first));
            binding.Configure(null, second, null);
            Assert.That(spy.Weapons, Is.SameAs(second));
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(other);
        }
    }

    [TestCase(1920f)]
    [TestCase(2400f)]
    public void ActionsUsesCanonicalDrawerAndCenteredTargetPanelInsideSafeArea(float width)
    {
        var root = new GameObject("ActionsSafeArea", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            var area = (RectTransform)root.transform;
            area.sizeDelta = new Vector2(width, 1080);
            var rect = WDUIFactory.Rect("Actions", area);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.gameObject.AddComponent<WDMatchActionsView>().Build(theme);
            var drawer = (RectTransform)rect.Find("ActionsDrawer");
            var panel = (RectTransform)rect.Find("WeaponTargetPopup/Panel");
            Assert.That(drawer.anchoredPosition, Is.EqualTo(new Vector2(295, -205)));
            Assert.That(drawer.sizeDelta, Is.EqualTo(new Vector2(396, 854)));
            Assert.That(drawer.GetComponent<Image>().color,
                Is.EqualTo(new Color(11f / 255, 14f / 255, 18f / 255, .95f)));
            Assert.That(drawer.GetComponent<Image>().sprite.border, Is.EqualTo(new Vector4(0, 13, 13, 13)),
                "Apenas os cantos direitos do drawer são arredondados.");
            Assert.That(drawer.Find("Collapse"), Is.Null);
            Assert.That(drawer.GetComponent<Button>(), Is.Not.Null);
            var outside = (RectTransform)rect.Find("ActionsOutside");
            Assert.That(outside.GetSiblingIndex(), Is.LessThan(drawer.GetSiblingIndex()));
            Assert.That(outside.GetComponent<Image>().color, Is.EqualTo(Color.clear));
            Assert.That(outside.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(outside.gameObject.activeSelf, Is.False);
            var viewport = (RectTransform)drawer.Find("ListViewport");
            Assert.That(viewport.anchoredPosition, Is.EqualTo(new Vector2(24, -99)));
            Assert.That(viewport.sizeDelta, Is.EqualTo(new Vector2(348, 731)));
            Assert.That(((RectTransform)drawer.Find("Title")).sizeDelta, Is.EqualTo(new Vector2(300, 34)));
            Assert.That(((RectTransform)drawer.Find("Count")).sizeDelta, Is.EqualTo(new Vector2(300, 17)));

            Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(1400, 620)));
            Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
            foreach (var item in new[] { drawer, panel })
            {
                var corners = new Vector3[4]; item.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector3 local = area.InverseTransformPoint(corner);
                    Assert.That(local.x, Is.InRange(area.rect.xMin, area.rect.xMax));
                    Assert.That(local.y, Is.InRange(area.rect.yMin, area.rect.yMax));
                }
            }
            Assert.That(rect.GetComponent<Graphic>(), Is.Null, "A raiz não captura eventos quando a camada externa está inativa.");
            Assert.That(panel.GetComponent<Button>(), Is.Not.Null, "Cliques internos são consumidos no painel.");
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(theme);
        }
    }

    // ============================================================
    // 04. SETE INSTALAÇÕES TERRITORIAIS E HUB CONFIGURÁVEL
    // ============================================================

    [Test]
    public void InstallationFamilyContainsExactlySevenAndRejectsRadarAndTransport()
    {
        string[] expected =
        {
            WDSupportWeaponIds.Bunker, WDSupportWeaponIds.Minefield,
            WDSupportWeaponIds.FlakTower, WDStrategicWeaponIds.EnergyPlant,
            WDStrategicWeaponIds.Artillery, WDStrategicWeaponIds.Rockets,
            WDSpecialWeaponIds.Drone
        };
        Assert.That(expected, Has.All.Matches<string>(
            WDInstallationHubFormatter.IsCanonicalInstallation));
        Assert.That(expected, Is.Unique);
        Assert.That(WDInstallationHubFormatter.IsCanonicalInstallation(
            WDTacticalWeaponIds.Radar), Is.False);
        Assert.That(WDInstallationHubFormatter.IsCanonicalInstallation(
            WDSupportWeaponIds.AirTransport), Is.False);
    }

    [TestCase(WDSupportWeaponIds.Bunker, false)]
    [TestCase(WDSupportWeaponIds.Minefield, false)]
    [TestCase(WDSupportWeaponIds.FlakTower, false)]
    [TestCase(WDStrategicWeaponIds.EnergyPlant, false)]
    [TestCase(WDStrategicWeaponIds.Artillery, false)]
    [TestCase(WDStrategicWeaponIds.Rockets, true)]
    [TestCase(WDSpecialWeaponIds.Drone, true)]
    public void InstallationHubSeparatesInformationFromCanonicalOperations(
        string weaponId, bool operational)
    {
        var installation = new WDWeaponInstallationState("id", weaponId, "territory",
            "p1", 3, 5, 2, 3, 2, true, 4);
        var snapshot = new WDWeaponHudInstallationState(installation, weaponId, operational, 5);
        WDInstallationHubPresentation model = WDInstallationHubFormatter.Format(snapshot);
        Assert.That(model.Sectors, Has.Length.EqualTo(3));
        Assert.That(model.IsOperational, Is.EqualTo(operational));
        Assert.That(model.CanOperate, Is.EqualTo(operational));
        Assert.That(model.AssetName, Is.Not.Empty);
        if (weaponId == WDStrategicWeaponIds.Artillery)
            StringAssert.Contains("AUTOMÁTICO", string.Join("|", model.Sectors));
    }

    [Test]
    public void InstallationHubValuesComeFromTheReadOnlyInstallationSnapshot()
    {
        WDInstallationHubPresentation bunker = Hub(WDSupportWeaponIds.Bunker,
            duration: 0, barrier: 5, charges: 0, payments: 0);
        WDInstallationHubPresentation mine = Hub(WDSupportWeaponIds.Minefield,
            duration: 0, barrier: 0, charges: 1, payments: 0);
        WDInstallationHubPresentation flak = Hub(WDSupportWeaponIds.FlakTower,
            duration: 3, barrier: 0, charges: 0, payments: 0);
        WDInstallationHubPresentation plant = Hub(WDStrategicWeaponIds.EnergyPlant,
            duration: 0, barrier: 0, charges: 0, payments: 3);
        WDInstallationHubPresentation artillery = Hub(WDStrategicWeaponIds.Artillery,
            duration: 0, barrier: 0, charges: 6, payments: 0);

        StringAssert.Contains("5 RESTANTE", bunker.Sectors[0]);
        StringAssert.Contains(WDTacticalWeaponTables.MineDamage(2).ToString(), mine.Sectors[0]);
        StringAssert.Contains("3 ROUNDS", flak.Sectors[0]);
        StringAssert.Contains("+" + WDStrategicWeaponTables.EnergyProduction(2), plant.Sectors[0]);
        StringAssert.Contains("3 RESTANTES", plant.Sectors[1]);
        StringAssert.Contains("ROUND 5", plant.Sectors[2]);
        StringAssert.Contains("6 / 6", artillery.Sectors[0]);
    }

    private static WDInstallationHubPresentation Hub(string weaponId, int duration,
        int barrier, int charges, int payments)
    {
        var state = new WDWeaponInstallationState("id-" + weaponId, weaponId, "territory",
            "p1", duration, barrier, charges, payments, 2, true, 4);
        return WDInstallationHubFormatter.Format(
            new WDWeaponHudInstallationState(state, weaponId, false, 4));
    }

    [Test]
    public void InstallationHubUsesFigmaGeometryBlockingOutsideAndIndependentActionTarget()
    {
        var root = new GameObject("InstallationHubFixture", typeof(RectTransform));
        var theme = ScriptableObject.CreateInstance<WarDominionUITheme>();
        try
        {
            root.AddComponent<WDMatchUIV2>().Build(theme, null, null, null);
            Transform layer = root.transform.Find("MatchInstallationHub");
            RectTransform hub = (RectTransform)layer.Find("InstallationHubRoot");
            Transform outside = layer.Find("InstallationHubOutside");
            Transform composition = hub.Find("ApprovedHubComposition");
            Assert.That(hub.sizeDelta, Is.EqualTo(new Vector2(480f, 413f)));
            Assert.That(composition, Is.Not.Null);
            Assert.That(((RectTransform)composition).sizeDelta, Is.EqualTo(new Vector2(448f, 413f)));
            Assert.That(hub.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(hub.GetComponent<Button>(), Is.Not.Null);
            Assert.That(outside.GetComponent<Image>().raycastTarget, Is.True);
            Assert.That(outside.GetComponent<Button>(), Is.Not.Null);
            Assert.That(composition.Find("CenterInfo").GetComponent<Button>(), Is.Not.Null);
            Assert.That(hub.gameObject.activeSelf, Is.False);
            Assert.That(outside.gameObject.activeSelf, Is.False);
            Assert.That(composition.Find("SectorTop"), Is.Not.Null);
            Assert.That(composition.Find("SectorMiddle"), Is.Not.Null);
            Assert.That(composition.Find("SectorBottom"), Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(theme);
        }
    }

    [Test]
    public void InstallationHubLoadsTheApprovedFigmaAssetFamily()
    {
        string[] assets =
        {
            "SegmentTop", "SegmentMiddle", "SegmentBottom", "Arc",
            "CenterInfo", "CenterAction", "Bunker", "Minefield", "FlakTower",
            "PowerPlant", "Artillery", "Rockets", "Drone"
        };
        foreach (string asset in assets)
            Assert.That(Resources.Load<Texture2D>(
                "UI/Match/FigmaCurrent/InstallationHubs/" + asset), Is.Not.Null, asset);
    }

    [Test]
    public void InstallationStackingIsStableAndLeavesIndependentTouchTargets()
    {
        Vector2[] first = new Vector2[7];
        for (int i = 0; i < first.Length; i++)
            first[i] = WDWeaponInstallationMapPresenter.StackOffset(i, first.Length);
        Assert.That(first, Is.Unique);
        for (int i = 0; i < first.Length; i++)
            Assert.That(WDWeaponInstallationMapPresenter.StackOffset(i, first.Length),
                Is.EqualTo(first[i]));
        Assert.That(WDWeaponInstallationMapPresenter.StackOffset(0, 1),
            Is.EqualTo(new Vector2(0f, 32f)));
    }

    [TestCase(WDSupportWeaponIds.Bunker, "Bunker")]
    [TestCase(WDSupportWeaponIds.Minefield, "Minefield")]
    [TestCase(WDSupportWeaponIds.FlakTower, "FlakTower")]
    [TestCase(WDStrategicWeaponIds.EnergyPlant, "PowerPlant")]
    [TestCase(WDStrategicWeaponIds.Artillery, "Artillery")]
    [TestCase(WDStrategicWeaponIds.Rockets, "Rockets")]
    [TestCase(WDSpecialWeaponIds.Drone, "Drone")]
    public void InstallationMarkerMapsEveryCanonicalWeaponToItsOfficialAsset(
        string weaponId, string asset)
    {
        Assert.That(WDWeaponInstallationMapPresenter.AssetNameFor(weaponId),
            Is.EqualTo(asset));
        Assert.That(Resources.Load<Texture2D>(
            "UI/Match/FigmaCurrent/InstallationHubs/" + asset), Is.Not.Null);
    }

    [Test]
    public void InstallationHubPlacementClampsToSafeAreaAndAvoidsHudWhenSpaceExists()
    {
        var safe = new Rect(-960f, -540f, 1920f, 1080f);
        var leftHud = new Rect(-948f, -345f, 252f, 710f);
        var rightHud = new Rect(714f, -141f, 218f, 282f);
        Vector2 size = new(333.899f, 414.189f);
        Vector2 placed = WDMatchInstallationHubView.ChooseContextualPosition(
            new Vector2(-760f, 280f), size, safe, leftHud, rightHud, 18f);
        var hub = new Rect(placed - size * .5f, size);
        Assert.That(safe.Contains(hub.min), Is.True);
        Assert.That(safe.Contains(hub.max), Is.True);
        Assert.That(hub.Overlaps(leftHud), Is.False);
        Assert.That(hub.Overlaps(rightHud), Is.False);
    }

    [Test]
    public void InstallationResolutionEventCarriesCanonicalTerritoryWithoutGameplayData()
    {
        ResolutionVisualEvent visual = ResolutionVisualEvent.InstallationImplemented(
            TerritorioClique.Dono.Jogador1, WDStrategicWeaponIds.EnergyPlant, "Alaska");
        Assert.That(visual.Type, Is.EqualTo(ResolutionVisualEventType.Installation));
        Assert.That(visual.DestinationTerritoryId, Is.EqualTo("Alaska"));
        Assert.That(visual.ContentId, Is.EqualTo(WDStrategicWeaponIds.EnergyPlant));
        Assert.That(visual.Amount, Is.Zero);
    }

    [TestCase(WDSupportWeaponIds.Minefield)]
    [TestCase(WDSupportWeaponIds.FlakTower)]
    public void SecretInstallationsDoNotExposePresentationToUnauthorizedViewer(string weaponId)
    {
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "owner", "enemy", false), Is.False);
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "owner", "owner", false), Is.True);
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            weaponId, "owner", "ally", true), Is.True);
        Assert.That(WDWeaponMatchUIController.IsInstallationVisibleTo(
            WDSupportWeaponIds.Bunker, "owner", "enemy", false), Is.True);
    }

    private sealed class PresentationSpy : IWDMatchUIPresentation, IWDMatchWeaponPresentation
    {
        public WDWeaponMatchUIController Weapons { get; private set; }
        public void BindWeapons(WDWeaponMatchUIController controller) => Weapons = controller;
        public int PresentCalls { get; private set; }
        public IWDMatchUtilityCommands Utilities { get; private set; }
        public void BindCommands(IMatchUICommands commands) { }
        public void BindUtilities(IWDMatchUtilityCommands utilities) =>
            Utilities = utilities;
        public void Present(MatchUIState state) => PresentCalls++;
        public void ShowInstallationHub(WDWeaponHudInstallationState installation) { }
    }
}
#endif
