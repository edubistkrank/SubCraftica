namespace SubCraftica.Services.Configuration;

/// <summary>
/// Helpers for Creative mode ingredient bypass rules.
/// Some items (like Ion Cubes) are exempt and must always be consumed normally,
/// mirroring the vanilla creative mode exception.
/// </summary>
internal static class CreativeModeHelper
{
    /// <summary>
    /// Returns true if the given TechType is exempt from Creative mode bypass
    /// (i.e. it must still be finite and consumed normally, even in Creative mode).
    /// </summary>
    internal static bool IsExemptFromCreativeBypass(TechType techType)
    {
        return techType == TechType.PrecursorIonCrystal;
    }

    /// <summary>
    /// Returns true if Creative mode is active AND the given ingredient is NOT exempt.
    /// Use this to decide whether to skip ingredient checks/consumption.
    /// </summary>
    internal static bool IsCreativeBypassActive(TechType techType)
    {
        if (Plugin.Services == null || !Plugin.Services.Config.CreativeMode.Value)
            return false;

        return !IsExemptFromCreativeBypass(techType);
    }
}
