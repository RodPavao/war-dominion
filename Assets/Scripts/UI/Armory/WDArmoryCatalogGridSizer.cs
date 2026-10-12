using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(GridLayoutGroup))]
public sealed class WDArmoryCatalogGridSizer : MonoBehaviour
{
    // ============================================================
    // 01. AUTORIDADE RESPONSIVA ÚNICA DA GRADE DO CATÁLOGO
    // ============================================================

    [SerializeField] private RectTransform viewport;
    [SerializeField] private float horizontalGap = 8f;
    [SerializeField] private float verticalGap = 10f;
    [SerializeField] private float minimumWidth = 170f;
    [SerializeField] private float maximumWidth = 190f;
    [SerializeField] private float heightRatio = 1.14f;

    private GridLayoutGroup grid;

    private void Awake()
    {
        grid = GetComponent<GridLayoutGroup>();
        ApplyAvailableWidth();
    }

    private void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled)
            ApplyAvailableWidth();
    }

    public void ApplyAvailableWidth()
    {
        grid ??= GetComponent<GridLayoutGroup>();
        if (grid == null || viewport == null)
            return;

        int safeColumns = Mathf.Max(1,
            Mathf.FloorToInt((viewport.rect.width + horizontalGap) / (minimumWidth + horizontalGap)));
        float usableWidth = Mathf.Max(0f, viewport.rect.width - horizontalGap * (safeColumns - 1));
        float width = Mathf.Clamp(usableWidth / safeColumns, minimumWidth, maximumWidth);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = safeColumns;
        grid.spacing = new Vector2(horizontalGap, verticalGap);
        grid.cellSize = new Vector2(width, Mathf.Round(width * heightRatio));
    }
}
