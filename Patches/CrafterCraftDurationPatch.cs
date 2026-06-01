using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch]
internal static class CrafterCraftDurationPatch
{
    [HarmonyPatch(typeof(Crafter), "Craft", new[] { typeof(TechType), typeof(float) })]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void CrafterCraftPrefix(Crafter __instance, ref float duration)
    {
        Plugin.Services?.TimeController.OnCrafterCraft(__instance, ref duration);
    }

    [HarmonyPatch(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) })]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static void GhostCrafterCraftPrefix(GhostCrafter __instance, ref float duration)
    {
        Plugin.Services?.TimeController.OnCrafterCraft(__instance, ref duration);
    }

    [HarmonyPatch(typeof(GhostCrafter), "OnCraftingBegin")]
    [HarmonyPostfix]
    private static void GhostCrafterOnCraftingBeginPostfix(GhostCrafter __instance)
    {
        Plugin.Services?.TimeController.OnCraftingBegin(__instance);
    }
}
