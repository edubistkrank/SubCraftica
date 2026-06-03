using HarmonyLib;
using SubCraftica.Services.UI;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Close))]
internal static class uGUICraftingMenuClosePatch
{
    [HarmonyPostfix]
    private static void Postfix(uGUI_CraftingMenu __instance)
    {
        if (Plugin.Services == null)
        {
            return;
        }

        RecipeOwnedIngredientsTooltipService.ResetTrack();

        if (Plugin.Services.Synchronization.IsCraftInProgress)
        {
            return;
        }

        var client = __instance != null ? __instance.client : null;
        if (client != null && client.inProgress)
        {
            return;
        }

        Plugin.Services.Queue.Clear();
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
