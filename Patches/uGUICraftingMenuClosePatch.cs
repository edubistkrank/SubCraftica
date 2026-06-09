using HarmonyLib;
using SubCraftica.Services.UI;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Close))]
internal static class uGUICraftingMenuClosePatch
{
    [HarmonyPrefix]
    private static bool Prefix(uGUI_CraftingMenu __instance)
    {
        if (Plugin.Services == null)
        {
            return true;
        }

        if (CraftingMenuClientHelper.IsConstructorClient(__instance))
        {
            return true;
        }

        if (!GameInput.GetButtonHeld(GameInput.Button.Sprint))
        {
            return true;
        }

        return false;
    }

    [HarmonyPostfix]
    private static void Postfix(uGUI_CraftingMenu __instance)
    {
        if (Plugin.Services == null)
        {
            return;
        }

        RecipeOwnedIngredientsTooltipService.ResetTrack();

        // Preserve queue on menu close. Closing UI should not cancel queued work.
        Plugin.Services.QueueCoordinator.ClearStopQueueContinuationRequested();
        Plugin.Services.QueueCoordinator.ResetForQueueEnd();
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
