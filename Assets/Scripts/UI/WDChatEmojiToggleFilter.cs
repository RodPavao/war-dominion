using UnityEngine;

// 01. Permite somente o toggle através da camada local; o backdrop global permanece separado.
public sealed class WDChatEmojiToggleFilter : MonoBehaviour, ICanvasRaycastFilter
{
    public RectTransform Target;
    public bool IsRaycastLocationValid(Vector2 point, Camera camera) => Target == null ||
        !RectTransformUtility.RectangleContainsScreenPoint(Target, point, camera);
}
