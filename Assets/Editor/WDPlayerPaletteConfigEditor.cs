using System.Globalization;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WDPlayerPaletteConfig))]
public sealed class WDPlayerPaletteConfigEditor : Editor
{
    // ============================================================
    // 01. PROPRIEDADES SERIALIZADAS DA CONFIGURAÇÃO GLOBAL
    // ============================================================

    private SerializedProperty colorHexCodes;
    private SerializedProperty defaultPlayerColorIndices;
    private SerializedProperty ownerlessTerritoryColor;

    private void OnEnable()
    {
        colorHexCodes = serializedObject.FindProperty("colorHexCodes");
        defaultPlayerColorIndices =
            serializedObject.FindProperty("defaultPlayerColorIndices");
        ownerlessTerritoryColor =
            serializedObject.FindProperty("ownerlessTerritoryColor");
    }

    // ============================================================
    // 02. LISTA VISUAL SINCRONIZADA COM O HEX PERSISTENTE
    // ============================================================

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField(
            "Cores oficiais dos jogadores", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "O HEX RGB é a fonte persistente exata. O seletor visual apenas " +
            "edita e sincroniza esse valor.", MessageType.Info);

        int requestedSize = Mathf.Max(0, EditorGUILayout.IntField(
            "Quantidade", colorHexCodes.arraySize));
        ResizePalette(requestedSize);

        for (int index = 0; index < colorHexCodes.arraySize; index++)
            DrawColorEntry(index, colorHexCodes.GetArrayElementAtIndex(index));

        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(
            defaultPlayerColorIndices,
            new GUIContent("Índice padrão por slot de jogador"), true);
        EditorGUILayout.PropertyField(
            ownerlessTerritoryColor,
            new GUIContent("Território sem dono"));

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawColorEntry(int index, SerializedProperty hexProperty)
    {
        string current = NormalizeHex(hexProperty.stringValue);
        bool valid = TryParseHex(current, out Color32 parsed);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"[{index}]", GUILayout.Width(32f));

        EditorGUI.BeginChangeCheck();
        Color selected = EditorGUILayout.ColorField(
            valid ? (Color)parsed : Color.white,
            GUILayout.Width(72f));
        if (EditorGUI.EndChangeCheck())
        {
            Color32 selectedBytes = selected;
            hexProperty.stringValue = ToHex(selectedBytes);
            current = hexProperty.stringValue;
            valid = true;
        }

        EditorGUI.BeginChangeCheck();
        string edited = EditorGUILayout.TextField(current).ToUpperInvariant();
        if (EditorGUI.EndChangeCheck())
        {
            hexProperty.stringValue = NormalizeHex(edited);
            valid = TryParseHex(hexProperty.stringValue, out _);
        }

        EditorGUILayout.EndHorizontal();

        if (!valid)
            EditorGUILayout.HelpBox(
                "HEX inválido. Use exatamente 6 caracteres RGB (0-9, A-F).",
                MessageType.Error);
        EditorGUILayout.EndVertical();
    }

    // ============================================================
    // 03. EXPANSÃO SEGURA E CONVERSÃO RGB EXATA
    // ============================================================

    private void ResizePalette(int requestedSize)
    {
        int previousSize = colorHexCodes.arraySize;
        if (requestedSize == previousSize)
            return;

        colorHexCodes.arraySize = requestedSize;
        for (int index = previousSize; index < requestedSize; index++)
            colorHexCodes.GetArrayElementAtIndex(index).stringValue = "FFFFFF";
    }

    private static string NormalizeHex(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().TrimStart('#').ToUpperInvariant();

    private static bool TryParseHex(string value, out Color32 color)
    {
        string normalized = NormalizeHex(value);
        color = new Color32(255, 255, 255, 255);
        if (normalized.Length != 6 ||
            !byte.TryParse(normalized.Substring(0, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out byte red) ||
            !byte.TryParse(normalized.Substring(2, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out byte green) ||
            !byte.TryParse(normalized.Substring(4, 2), NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out byte blue))
        {
            return false;
        }

        color = new Color32(red, green, blue, 255);
        return true;
    }

    private static string ToHex(Color32 color) =>
        $"{color.r:X2}{color.g:X2}{color.b:X2}";
}
