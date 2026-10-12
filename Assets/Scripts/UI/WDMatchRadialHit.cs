using UnityEngine;

public sealed class WDMatchRadialHit : MonoBehaviour, ICanvasRaycastFilter
{
    public Vector2 Center;
    public float StartAngle, EndAngle;
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        RectTransform rect=transform as RectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screenPoint,eventCamera,out Vector2 local)) return false;
        Vector2 delta=local-new Vector2(Center.x,-Center.y);
        float angle=Mathf.Repeat(Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,360);
        return delta.magnitude>=138 && delta.magnitude<=338 && angle>=StartAngle && angle<=EndAngle;
    }
}
