using UnityEngine;

namespace SubCraftica.Services.UI;

internal static class ColorHexUtility
{
    internal static string ToHex(Color c)
    {
        var r = Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
        var g = Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
        var b = Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
        var a = Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
        return r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + a.ToString("X2");
    }
}
