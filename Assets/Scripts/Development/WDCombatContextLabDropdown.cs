using TMPro;
using UnityEngine;

/// <summary>
/// Dropdown exclusivo do laboratório, com recuperação do estado transitório
/// do fade após recompilações durante o Play Mode.
/// </summary>
public sealed class WDCombatContextLabDropdown : TMP_Dropdown
{
    [System.NonSerialized] private bool tweenLifecycleInitialized;

    // 01. CICLO DE VIDA TMP — o runner de fade não é serializado pelo pacote.
    // A inicialização é repetida apenas após um reload de domínio, usando a
    // implementação protegida oficial da própria classe base.
    protected override void OnEnable()
    {
        base.OnEnable();
        if (Application.isPlaying && !tweenLifecycleInitialized)
            InitializeTweenLifecycle();
    }

    protected override void Start()
    {
        if (!tweenLifecycleInitialized)
            InitializeTweenLifecycle();
    }

    private void InitializeTweenLifecycle()
    {
        base.Start();
        tweenLifecycleInitialized = true;
    }
}
