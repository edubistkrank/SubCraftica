using HarmonyLib;
using SubCraftica.Services.Crafting;
using SubCraftica.Services.Logging;

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

        var prototypeCompat = Plugin.Services.PrototypeSubCompat;
        var isPrototypeClient = prototypeCompat != null && prototypeCompat.IsPrototypeCrafterClientActive();
        if (isPrototypeClient)
        {
            var clientType = __instance?.client?.GetType().FullName ?? "null";
            PrototypeCompatDebugLogger.Info($"Menu.Action request techType={techType} clientType={clientType}");
        }

        var defabCompat = Plugin.Services.DefabricatorCompat;
        var isDefabRecycle = defabCompat != null && defabCompat.IsDefabricationActiveFor(techType);

        if (!Plugin.Services.Config.CreativeMode.Value && !isDefabRecycle)
        {
            var unlockState = KnownTech.GetTechUnlockState(techType);
            if (isPrototypeClient)
            {
                PrototypeCompatDebugLogger.Debug($"Menu.Action unlockState={unlockState} techType={techType}");
            }

            if (unlockState != TechUnlockState.Available)
            {
                if (isPrototypeClient)
                {
                    PrototypeCompatDebugLogger.Warn($"Menu.Action blocked by unlockState={unlockState} techType={techType}");
                }
                return false;
            }
        }

        var amount = Plugin.Services.Quantity.GetCurrentAmount(techType);
        var request = new CraftingRequest(techType, amount);

        if (!Plugin.Services.PlannerValidation.CanPlan(techType))
        {
            if (isPrototypeClient)
            {
                PrototypeCompatDebugLogger.Warn($"Menu.Action planner validation failed techType={techType}");
            }
            return true;
        }

        if (!Plugin.Services.Config.CreativeMode.Value)
        {
            if (isDefabRecycle)
            {
                if (!defabCompat.CanRecycleAmount(techType, request.Amount))
                {
                    if (isPrototypeClient)
                    {
                        PrototypeCompatDebugLogger.Warn($"Menu.Action defab cannot recycle amount={request.Amount} techType={techType}");
                    }
                    ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
                    return false;
                }
            }
            else
            {
                var plan = Plugin.Services.RecipePlanner.BuildRequestPlan(techType, request.Amount);
                if (isPrototypeClient)
                {
                    PrototypeCompatDebugLogger.Debug($"Menu.Action BuildRequestPlan success={plan.Success} amount={request.Amount} techType={techType}");
                }

                if (!plan.Success)
                {
                    if (isPrototypeClient)
                    {
                        PrototypeCompatDebugLogger.Warn($"Menu.Action blocked by missing ingredients amount={request.Amount} techType={techType}");
                    }
                    ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
                    return false;
                }
            }
        }

        var queued = Plugin.Services.Queue.TryEnqueue(request, Plugin.Services.Config.MaxQueueSize.Value);
        if (!queued)
        {
            if (isPrototypeClient)
            {
                PrototypeCompatDebugLogger.Warn($"Menu.Action queue full amount={request.Amount} techType={techType} queueCount={Plugin.Services.Queue.Count}");
            }
            Plugin.Services.QueueFeedback.NotifyQueueFull(Plugin.Services.Config.MaxQueueSize.Value);
            return false;
        }

        if (isPrototypeClient)
        {
            PrototypeCompatDebugLogger.Info($"Menu.Action queued amount={request.Amount} totalAmount={request.TotalAmount} techType={techType} queueCount={Plugin.Services.Queue.Count}");
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
            if (isPrototypeClient)
            {
                PrototypeCompatDebugLogger.Debug($"Menu.Action deferred immediate craft syncInProgress={Plugin.Services.Synchronization.IsCraftInProgress} menuCrafting={isMenuClientCraftingInProgress}");
            }
            return false;
        }

        if (isPrototypeClient)
        {
            PrototypeCompatDebugLogger.Debug("Menu.Action allowing immediate craft start");
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