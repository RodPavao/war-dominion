using UnityEngine;

[CreateAssetMenu(fileName = "WDArmoryV2PrefabProvider", menuName = "War Dominion/UI/Armory V2 Prefab Provider")]
public sealed class WDArmoryV2PrefabProvider : ScriptableObject
{
    // ============================================================
    // 01. REFERÊNCIA SERIALIZADA ÚNICA PARA A INTERFACE OFICIAL V2
    // ============================================================
    [SerializeField] private WDArmoryScreenV2View screenPrefab;

    public WDArmoryScreenV2View ScreenPrefab => screenPrefab;
}
