using HarmonyLib;
using SubCraftica.Services.Crafting;
using SubCraftica.Services.Localization;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(uGUI_CraftingMenu), "Action")]
internal static class uGUICraftingMenuActionPatch
{
    [HarmonyPrefix]
    private static bool Prefix(uGUI_CraftingMenu __instance, object sender)
    {
        if (Plugin.Services == null)
        {
            return true;
        }

        if (CraftingMenuClientHelper.IsConstructorClient(__instance))
        {
            return true;
        }

        if (Plugin.Services.Synchronization.IsCraftInProgress)
        {
            ErrorMessage.AddWarning(ModText.Get(ModText.CraftInProgress));
            return false;
        }

        if (!TryResolveTechType(sender, out var techType))
        {
            return true;
        }

        var amount = Plugin.Services.Quantity.GetCurrentAmount(techType);
        var request = new CraftingRequest(techType, amount);

        if (!Plugin.Services.PlannerValidation.CanPlan(techType))
        {
            return true;
        }

        var plan = Plugin.Services.RecipePlanner.BuildRequestPlan(techType, request.Amount);
        if (!plan.Success)
        {
            ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
            return false;
        }

        var queued = Plugin.Services.Queue.TryEnqueue(request, Plugin.Services.Config.MaxQueueSize.Value);
        if (!queued)
        {
            if (Plugin.Services.Queue.TryPeek(out var head) && head != null && head.TechType != techType)
            {
                Plugin.Services.QueueFeedback.NotifyQueueMismatch(head.TechType, techType);
            }
            else
            {
                Plugin.Services.QueueFeedback.NotifyQueueFull(Plugin.Services.Config.MaxQueueSize.Value);
            }

            return false;
        }

        Plugin.Services.QueueFeedback.NotifyQueued(request);
        return true;
    }

    private static bool TryResolveTechType(object sender, out TechType techType)
    {
        techType = TechType.None;
        if (sender == null)
        {
            return false;
        }

        var field = sender.GetType().GetField("techType");
        if (field == null)
        {
            return false;
        }

        var value = field.GetValue(sender);
        if (!(value is TechType type))
        {
            return false;
        }

        techType = type;
        return techType != TechType.None;
    }
}

internal static class CraftingMenuClientHelper
{
    internal static bool IsConstructorClient(object client)
    {
        return client is ConstructorInput || client is RocketConstructorInput;
    }

    internal static bool IsConstructorClient(uGUI_CraftingMenu menu)
    {
        return IsConstructorClient(menu != null ? menu.client : null);
    }

    internal static bool IsConstructorClientActive()
    {
        var menu = uGUI.main != null ? uGUI.main.craftingMenu : null;
        return IsConstructorClient(menu);
    }
}