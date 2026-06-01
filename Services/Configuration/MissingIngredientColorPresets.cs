using System.Reflection;
using UnityEngine;

namespace SubCraftica.Services.Configuration;

internal static class MissingIngredientColorPresets
{
    private static readonly FieldInfo CurrentLanguageField = typeof(Language).GetField("currentLanguage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static readonly Color[] Colors =
    {
        new Color(0.87f, 0.25f, 0.15f, 1f), // 0 Vanilla red
        new Color(0.65f, 0.10f, 0.05f, 1f), // 1 Dark red
        new Color(0.95f, 0.50f, 0.45f, 1f), // 2 Soft red
        new Color(0.95f, 0.50f, 0.15f, 1f), // 3 Orange
        new Color(0.72f, 0.35f, 0.08f, 1f), // 4 Dark orange
        new Color(0.95f, 0.75f, 0.20f, 1f), // 5 Amber warning
        new Color(0.72f, 0.55f, 0.10f, 1f), // 6 Dark amber
        new Color(0.75f, 0.30f, 0.65f, 1f), // 7 Magenta
        new Color(0.55f, 0.20f, 0.48f, 1f), // 8 Dark magenta
        new Color(0.65f, 0.65f, 0.65f, 1f)  // 9 Muted gray
    };

    public static readonly string[] ChoiceLabels =
    {
        GetLocalizedLabel("DE3F26FF", "Vanilla red", "Rojo vanilla"),
        GetLocalizedLabel("A61A0DFF", "Dark red", "Rojo oscuro"),
        GetLocalizedLabel("F27F72FF", "Soft red", "Rojo suave"),
        GetLocalizedLabel("F27F26FF", "Orange", "Naranja"),
        GetLocalizedLabel("B85914FF", "Dark orange", "Naranja oscuro"),
        GetLocalizedLabel("F2BF33FF", "Amber", "Ámbar"),
        GetLocalizedLabel("B88B1AFF", "Dark amber", "Ámbar oscuro"),
        GetLocalizedLabel("BF4DA6FF", "Magenta", "Magenta"),
        GetLocalizedLabel("8C337AFF", "Dark magenta", "Magenta oscuro"),
        GetLocalizedLabel("A6A6A6FF", "Muted gray", "Gris apagado")
    };

    private static string GetLocalizedLabel(string colorHex, string english, string spanish)
    {
        var language = Language.main != null ? CurrentLanguageField?.GetValue(Language.main)?.ToString() : string.Empty;
        var label = string.Equals(language, "Spanish", System.StringComparison.OrdinalIgnoreCase) ? spanish : english;
        return $"<color=#{colorHex}>■</color> {label}";
    }
}
