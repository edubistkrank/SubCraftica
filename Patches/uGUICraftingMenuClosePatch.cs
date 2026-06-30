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

        if (!CraftingMenuSprintLatch.IsSprintHeldVirtual())
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
        uGUIPinnedRecipesUpdateIngredientsPatch.ClearPingCache();
        CraftingMenuSprintLatch.Clear();

        // Preserve queue on menu close. Closing UI should not cancel queued work.
        Plugin.Services.QueueCoordinator.ClearStopQueueContinuationRequested();
        Plugin.Services.QueueCoordinator.ResetForQueueEnd();
    }
}

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Close), new[] { typeof(ITreeActionReceiver) })]
internal static class uGUICraftingMenuCloseReceiverPatch
{
    [HarmonyPrefix]
    private static bool Prefix(uGUI_CraftingMenu __instance, ITreeActionReceiver receiver)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return true;
        }

        if (!ReferenceEquals(__instance.client as object, receiver as object))
        {
            return true;
        }

        if (!CraftingMenuSprintLatch.IsSprintHeldVirtual())
        {
            return true;
        }

        if (!ReferenceEquals(__instance.client as object, uGUI.main?.craftingMenu?.client as object))
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Lock))]
internal static class uGUICraftingMenuLockReceiverPatch
{
    [HarmonyPrefix]
    private static bool Prefix(uGUI_CraftingMenu __instance, ITreeActionReceiver receiver)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return true;
        }

        if (!ReferenceEquals(__instance.client as object, receiver as object))
        {
            return true;
        }

        if (!CraftingMenuSprintLatch.IsSprintHeldVirtual())
        {
            return true;
        }

        if (!ReferenceEquals(__instance.client as object, uGUI.main?.craftingMenu?.client as object))
        {
            return true;
        }

        return false;
    }
}

[HarmonyPatch(typeof(uGUI_CraftingMenu), nameof(uGUI_CraftingMenu.Open))]
internal static class uGUICraftingMenuOpenPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        RecipeOwnedIngredientsTooltipService.Prewarm();
        uGUIPinnedRecipesUpdateIngredientsPatch.ClearPingCache();

        if (Plugin.Services == null)
        {
            return;
        }
    }
}
