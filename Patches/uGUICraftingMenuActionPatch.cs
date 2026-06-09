using HarmonyLib;
using SubCraftica.Services.Crafting;

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

        if (!TryResolveTechType(sender, out var techType))
        {
            return true;
        }

        var defabCompat = Plugin.Services.DefabricatorCompat;
        var isDefabRecycle = defabCompat != null && defabCompat.IsDefabricationActiveFor(techType);

        if (!Plugin.Services.Config.CreativeMode.Value && !isDefabRecycle)
        {
            var unlockState = KnownTech.GetTechUnlockState(techType);
            if (unlockState != TechUnlockState.Available)
            {
                return false;
            }
        }

        var amount = Plugin.Services.Quantity.GetCurrentAmount(techType);
        var request = new CraftingRequest(techType, amount);

        if (!Plugin.Services.PlannerValidation.CanPlan(techType))
        {
            return true;
        }

        if (!Plugin.Services.Config.CreativeMode.Value)
        {
            if (isDefabRecycle)
            {
                if (!defabCompat.CanRecycleAmount(techType, request.Amount))
                {
                    ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
                    return false;
                }
            }
            else
            {
                var plan = Plugin.Services.RecipePlanner.BuildRequestPlan(techType, request.Amount);
                if (!plan.Success)
                {
                    ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
                    return false;
                }
            }
        }

        var queued = Plugin.Services.Queue.TryEnqueue(request, Plugin.Services.Config.MaxQueueSize.Value);
        if (!queued)
        {
            Plugin.Services.QueueFeedback.NotifyQueueFull(Plugin.Services.Config.MaxQueueSize.Value);
            return false;
        }

        // Clear focus so the next hover on this item starts at x1
        Plugin.Services.Quantity.ResetFocus(techType);

        var isMenuClientCraftingInProgress = IsMenuClientCraftingInProgress(__instance);

        // Register pending progress as soon as the request enters the queue.
        // Using Count >= 1 avoids missing the immediate next x1 enqueue in short craft-state windows.
        var isPending = Plugin.Services.Synchronization.IsCraftInProgress
                     || isMenuClientCraftingInProgress
                     || Plugin.Services.Queue.Count >= 1;
        Plugin.Services.QueueFeedback.NotifyQueued(request, isPending);

        if (Plugin.Services.Synchronization.IsCraftInProgress || isMenuClientCraftingInProgress)
        {
            return false;
        }

        return true;
    }

    private static bool IsMenuClientCraftingInProgress(uGUI_CraftingMenu menu)
    {
        var client = menu != null ? menu.client : null;
        if (client == null)
        {
            return false;
        }

        try
        {
            var craftingField = AccessTools.Field(client.GetType(), "crafting");
            if (craftingField == null)
            {
                return false;
            }

            var value = craftingField.GetValue(client);
            return value is bool isCrafting && isCrafting;
        }
        catch
        {
            return false;
        }
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

[HarmonyPatch(typeof(uGUI_CraftingMenu), "SetLocked")]
internal static class uGUICraftingMenuSetLockedPatch
{
    [HarmonyPrefix]
    private static void Prefix(uGUI_CraftingMenu __instance, ref bool locked)
    {
        if (!locked || Plugin.Services == null)
        {
            return;
        }

        if (CraftingMenuClientHelper.IsConstructorClient(__instance))
        {
            return;
        }

        if (!GameInput.GetButtonHeld(GameInput.Button.Sprint))
        {
            return;
        }

        locked = false;
    }
}