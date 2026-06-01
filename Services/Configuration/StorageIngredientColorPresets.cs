using System.Reflection;
using UnityEngine;

namespace SubCraftica.Services.Configuration;

internal static class StorageIngredientColorPresets
{
    private static readonly FieldInfo CurrentLanguageField = typeof(Language).GetField("currentLanguage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static readonly Color[] Colors =
    {
        new Color(0.58f, 0.87f, 0f, 1f),    // 0 Vanilla green
        new Color(0.36f, 0.62f, 0f, 1f),    // 1 Dark green
        new Color(0.66f, 0.89f, 0.30f, 1f), // 2 Soft green
        new Color(0.63f, 0.85f, 0.75f, 1f), // 3 Mint
        new Color(0.40f, 0.66f, 0.56f, 1f), // 4 Dark mint
        new Color(0.95f, 0.84f, 0.42f, 1f), // 5 Warm yellow
        new Color(0.76f, 0.63f, 0.24f, 1f), // 6 Dark yellow
        new Color(0.53f, 0.80f, 0.95f, 1f), // 7 Soft blue
        new Color(0.31f, 0.55f, 0.78f, 1f), // 8 Dark blue
        new Color(0.73f, 0.66f, 0.93f, 1f), // 9 Soft violet
        new Color(0.53f, 0.45f, 0.76f, 1f), // 10 Dark violet
        new Color(0.95f, 0.68f, 0.60f, 1f), // 11 Soft coral
        new Color(0.76f, 0.46f, 0.40f, 1f), // 12 Dark coral
        new Color(0.88f, 0.76f, 0.56f, 1f), // 13 Soft amber
        new Color(0.69f, 0.54f, 0.34f, 1f), // 14 Dark amber
        new Color(0.80f, 0.80f, 0.80f, 1f), // 15 Soft gray
        new Color(0.56f, 0.56f, 0.56f, 1f)  // 16 Dark gray
    };

    public static readonly string[] ChoiceLabels =
    {
        GetLocalizedLabel("94DE00FF", "Vanilla green", "Verde vanilla"),
        GetLocalizedLabel("5C9E00FF", "Dark green", "Verde oscuro"),
        GetLocalizedLabel("A8E34DFF", "Soft green", "Verde suave"),
        GetLocalizedLabel("A1D9BFFF", "Mint", "Menta"),
        GetLocalizedLabel("66A88FFF", "Dark mint", "Menta oscura"),
        GetLocalizedLabel("F2D66BFF", "Warm yellow", "Amarillo cálido"),
        GetLocalizedLabel("C2A03DFF", "Dark yellow", "Amarillo oscuro"),
        GetLocalizedLabel("87CCF2FF", "Soft blue", "Azul suave"),
        GetLocalizedLabel("4F8CC7FF", "Dark blue", "Azul oscuro"),
        GetLocalizedLabel("BBA8EDFF", "Soft violet", "Violeta suave"),
        GetLocalizedLabel("8773C2FF", "Dark violet", "Violeta oscuro"),
        GetLocalizedLabel("F2AD99FF", "Soft coral", "Coral suave"),
        GetLocalizedLabel("C27466FF", "Dark coral", "Coral oscuro"),
        GetLocalizedLabel("E0C28FFF", "Soft amber", "Ámbar suave"),
        GetLocalizedLabel("B08757FF", "Dark amber", "Ámbar oscuro"),
        GetLocalizedLabel("CCCCCCFF", "Soft gray", "Gris suave"),
        GetLocalizedLabel("8F8F8FFF", "Dark gray", "Gris oscuro")
    };

    private static string GetLocalizedLabel(string colorHex, string english, string spanish)
    {
        var language = Language.main != null ? CurrentLanguageField?.GetValue(Language.main)?.ToString() : string.Empty;
        var label = string.Equals(language, "Spanish", System.StringComparison.OrdinalIgnoreCase) ? spanish : english;
        return $"<color=#{colorHex}>■</color> {label}";
    }
}
