using System;
using UnityEngine;

public enum WDMatchUtilityModule
{
    None,
    Menu,
    Regions,
    Chat,
    Settings
}

public interface IWDMatchUtilityCommands
{
    WDMatchUtilityModule ActiveModule { get; }
    event Action<WDMatchUtilityModule> ModuleRequested;
    void Toggle(WDMatchUtilityModule module);
    void CloseActive();
}

public sealed class WDMatchUtilityCommands : IWDMatchUtilityCommands
{
    // ============================================================
    // 01. AUTORIDADE ÚNICA DAS UTILIDADES DA PARTIDA
    // ============================================================

    private readonly Action toggleRegions;
    private readonly Func<bool> regionsAreActive;

    public WDMatchUtilityModule ActiveModule { get; private set; }
    public event Action<WDMatchUtilityModule> ModuleRequested;

    public WDMatchUtilityCommands(
        WDMatchInformationalOverlayController informationalOverlay)
        : this(
            () => informationalOverlay?.ToggleRegions(),
            () => informationalOverlay?.RegionsActive == true)
    {
    }

    public WDMatchUtilityCommands(Action toggleRegions,
        Func<bool> regionsAreActive)
    {
        this.toggleRegions = toggleRegions;
        this.regionsAreActive = regionsAreActive;
    }

    // ============================================================
    // 02. TOGGLE EXCLUSIVO E ROTAS PARA OS PRÓXIMOS MÓDULOS
    // ============================================================

    public void Toggle(WDMatchUtilityModule module)
    {
        if (module == WDMatchUtilityModule.None)
            return;

        if (ActiveModule == module)
        {
            CloseActive();
            return;
        }

        CloseActive();
        ActiveModule = module;

        if (module == WDMatchUtilityModule.Regions)
        {
            toggleRegions?.Invoke();
            if (regionsAreActive?.Invoke() != true)
            {
                ActiveModule = WDMatchUtilityModule.None;
                return;
            }
        }

        ModuleRequested?.Invoke(module);
    }

    public void CloseActive()
    {
        bool hadActiveModule = ActiveModule != WDMatchUtilityModule.None;
        if (ActiveModule == WDMatchUtilityModule.Regions &&
            regionsAreActive?.Invoke() == true)
        {
            toggleRegions?.Invoke();
        }

        ActiveModule = WDMatchUtilityModule.None;
        if (hadActiveModule)
            ModuleRequested?.Invoke(WDMatchUtilityModule.None);
    }
}

public interface IWDMatchUIPresentation
{
    void BindCommands(IMatchUICommands commands);
    void BindUtilities(IWDMatchUtilityCommands utilities);
    void Present(MatchUIState state);
    void ShowInstallationHub(WDWeaponHudInstallationState installation);
}

// 04. Dados compartilhados opcionais: a apresentação não cria outro repositório.
public interface IWDMatchChatPresentation
{
    void BindChat(WDChatRepository repository, WDChatChannel matchChannel);
}

public interface IWDMatchWeaponPresentation
{
    void BindWeapons(WDWeaponMatchUIController controller);
}

public interface IWDMatchInteractionPresentation
{
    void BindInformational(WDMatchInformationalOverlayController controller);
}

[DisallowMultipleComponent]
public sealed class WDMatchUIPresentationBinding : MonoBehaviour
{
    // 1. Dependências funcionais expostas à futura Match UI V2.
    public IMatchUICommands Commands { get; private set; }
    public WDWeaponMatchUIController WeaponUI { get; private set; }
    public WDMatchInformationalOverlayController InformationalOverlay { get; private set; }
    public IWDMatchUtilityCommands Utilities { get; private set; }
    public MatchUIState LatestState { get; private set; }

    private IWDMatchUIPresentation presentation;
    private WDChatRepository chat;
    private WDChatChannel matchChat;

    public void ConfigureChat(WDChatRepository repository, WDChatChannel channel)
    {
        chat = repository;
        matchChat = channel;
        (presentation as IWDMatchChatPresentation)?.BindChat(chat, matchChat);
    }

    public void Configure(IMatchUICommands commands,
        WDWeaponMatchUIController weaponUI,
        WDMatchInformationalOverlayController informationalOverlay,
        IWDMatchUtilityCommands utilities = null)
    {
        Commands = commands;
        WeaponUI = weaponUI;
        InformationalOverlay = informationalOverlay;
        Utilities = utilities;
        presentation?.BindCommands(commands);
        presentation?.BindUtilities(utilities);
        (presentation as IWDMatchWeaponPresentation)?.BindWeapons(weaponUI);
        (presentation as IWDMatchInteractionPresentation)?.BindInformational(informationalOverlay);
    }

    // 2. Uma única apresentação pode possuir a tela por vez.
    public void Register(IWDMatchUIPresentation next)
    {
        presentation = next;
        if (presentation == null)
            return;
        presentation.BindCommands(Commands);
        presentation.BindUtilities(Utilities);
        (presentation as IWDMatchWeaponPresentation)?.BindWeapons(WeaponUI);
        (presentation as IWDMatchInteractionPresentation)?.BindInformational(InformationalOverlay);
        (presentation as IWDMatchChatPresentation)?.BindChat(chat, matchChat);
        if (LatestState != null)
            presentation.Present(LatestState);
    }

    public void Present(MatchUIState state)
    {
        LatestState = state;
        presentation?.Present(state);
    }

    // 3. Hubs visuais são opcionais; a regra das instalações permanece no controller.
    public void ShowInstallationHub(WDWeaponHudInstallationState installation) =>
        presentation?.ShowInstallationHub(installation);
}
