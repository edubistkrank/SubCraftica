using System;
using HarmonyLib;

namespace SubCraftica.Patches.Compat;

[HarmonyPatch(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) })]
internal static class PrototypeSubGhostCrafterCraftCompatPatch
{
    [HarmonyPrefix]
    private static void Prefix(GhostCrafter __instance, TechType techType)
    {
        if (Plugin.Services?.PrototypeSubCompat == null)
        {
            return;
        }

        if (!Plugin.Services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
        {
            return;
        }

        PrototypeSubGhostCrafterCompatState.LastPrototypeCrafter = __instance;
        Plugin.Log?.LogDebug($"[Compat/PrototypeSub] Craft request intercepted for {techType} on AlienFabricator.");
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, GhostCrafter __instance)
    {
        if (ReferenceEquals(PrototypeSubGhostCrafterCompatState.LastPrototypeCrafter, __instance))
        {
            PrototypeSubGhostCrafterCompatState.LastPrototypeCrafter = null;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(GhostCrafter), "OnCraftingEnd")]
internal static class PrototypeSubGhostCrafterCraftingEndCompatPatch
{
    [HarmonyPrefix]
    private static bool Prefix(GhostCrafter __instance)
    {
        if (Plugin.Services?.PrototypeSubCompat == null)
        {
            return true;
        }

        if (!Plugin.Services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
        {
            return true;
        }

        var tracked = PrototypeSubGhostCrafterCompatState.LastPrototypeCrafter;
        if (tracked != null && !ReferenceEquals(tracked, __instance))
        {
            Plugin.Log?.LogDebug("[Compat/PrototypeSub] Skipping OnCraftingEnd continuation because active AlienFabricator changed.");
            return false;
        }

        Plugin.Log?.LogDebug("[Compat/PrototypeSub] Handling per-item queue continuation on AlienFabricator.");
        return true;
    }
}

internal static class PrototypeSubGhostCrafterCompatState
{
    internal static GhostCrafter LastPrototypeCrafter { get; set; }
}
