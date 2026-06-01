using HarmonyLib;
using SubCraftica.Services.UI;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Close))]
internal static class uGUICraftingMenuClosePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        if (Plugin.Services == null)
        {
            return;
        }

        RecipeOwnedIngredientsTooltipService.ResetTrack();
    }
}

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Open))]
internal static class uGUICraftingMenuOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RecipeOwnedIngredientsTooltipService.Prewarm();
    }
}
