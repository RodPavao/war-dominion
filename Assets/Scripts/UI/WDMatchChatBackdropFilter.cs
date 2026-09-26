using UnityEngine;

// 01. Mantém os toggles existentes acessíveis sem liberar cliques para o mapa.
public sealed class WDMatchChatBackdropFilter : MonoBehaviour, ICanvasRaycastFilter
{
    public RectTransform Panel { get; set; }
    public RectTransform[] UtilityTargets { get; set; }

    public bool IsRaycastLocationValid(Vector2 point, Camera eventCamera)
    {
        if (Panel != null && RectTransformUtility.RectangleContainsScreenPoint(Panel, point, eventCamera))
            return true;
        if (UtilityTargets != null)
            foreach (RectTransform target in UtilityTargets)
                if (target != null && target.gameObject.activeInHierarchy &&
                    RectTransformUtility.RectangleContainsScreenPoint(target, point, eventCamera))
                    return false;
        return true;
    }
}
