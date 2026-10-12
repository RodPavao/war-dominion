using UnityEngine;

// Valores equivalentes derivados do Chat Geral aprovado; nenhum layout/contexto.
public static class WDChatVisualStyle
{
    public const float Message = 20, Placeholder = 18, Preview = 14, Name = 15;
    public const float Heading = 22, Status = 14, Timestamp = 12, Control = 14, Section = 12;
    public const float ReadableAlpha = .95f;
    public static readonly Color SelectedConversation = new(17f / 255, 35f / 255, 45f / 255, .8f);
}
