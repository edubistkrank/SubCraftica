using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Resources;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(GhostCrafter), "OnCraftingBegin")]
internal static class GhostCrafterCraftingBeginPatch
{
    [HarmonyPostfix]
    private static void Postfix(GhostCrafter __instance)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return;
        }

        if (!Plugin.Services.Runtime.TryGetLastTechType(out var techType))
        {
            return;
        }

        if (!Plugin.Services.CraftRuntimeState.TryGetRequiredEnergy(techType, out var requiredEnergy))
        {
            requiredEnergy = Plugin.Services.Energy.GetRequiredEnergy(techType, 1);
        }

        var powerRelay = Traverse.Create(__instance).Field<PowerRelay>("powerRelay").Value;
        if (Plugin.Services.Energy.TryConsumePlannedEnergy(powerRelay, techType, requiredEnergy))
        {
            Plugin.Services.QueueCoordinator.ClearStopQueueContinuationRequested();
            return;
        }

        Plugin.Services.QueueCoordinator.RequestStopQueueContinuation(Plugin.Services.Queue);

        ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
        __instance.CancelInvoke("Craft");
    }
}

[HarmonyPatch(typeof(GhostCrafter), "OnCraftingEnd")]
internal static class GhostCrafterCraftingEndPatch
{
    private const string CraftingMenuLockWarningKey = "GhostCrafterCraftingEnd.SetLocked";
    private static readonly MethodInfo GhostCrafterCraftMethod = AccessTools.Method(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) });
    private static readonly MethodInfo CraftingMenuSetLockedMethod = AccessTools.Method(typeof(uGUI_CraftingMenu), "SetLocked");
    private static readonly MethodInfo CrafterHasCraftedItemMethod = AccessTools.Method(typeof(Crafter), "HasCraftedItem");

    [HarmonyPrefix]
    private static void Prefix(GhostCrafter __instance)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return;
        }

        // Vanilla only calls logic.TryPickup() in OnCraftingEnd when
        // PlayerIsInRange(closeDistance) || pickupOutOfRange.
        // Force this path so crafted items are pushed immediately even when the player is far.
        __instance.pickupOutOfRange = true;
    }

    [HarmonyPostfix]
    private static void Postfix(GhostCrafter __instance)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return;
        }

        // Extra safety: ensure pickup attempt also runs from our side
        // in case another mod short-circuits vanilla flow.
        var logic = Traverse.Create(__instance).Field<CrafterLogic>("logic").Value;
        if (logic != null)
        {
            logic.TryPickup();
        }

        if (Plugin.Services.Config.CraftingMode.Value != ModConfig.CraftingModePerItem)
        {
            return;
        }

        __instance.StartCoroutine(HandlePerItemQueueEnd(__instance));
    }

    private static IEnumerator HandlePerItemQueueEnd(GhostCrafter crafter)
    {
        // Consume the techType that just finished — must read it before first yield
        // because the Finalizer in GhostCrafterCraftPatch clears Runtime.lastTechType
        // before OnCraftingEnd fires.
        var finishedTechType = Plugin.Services.Runtime.ConsumeLastPerItemFinished();

        yield return null;

        if (crafter == null || Plugin.Services == null)
        {
            yield break;
        }

        var waitFrames = 0;
        while (Plugin.Services.Synchronization.IsCraftInProgress && waitFrames < 5)
        {
            waitFrames++;
            yield return null;
        }

        waitFrames = 0;
        while (Plugin.Services.QueueCoordinator.HasPendingPickupOperations && waitFrames < 30)
        {
            waitFrames++;
            yield return null;
        }

        // Resolve crafted output before queue continuation.
        if (HasCraftedItem(crafter))
        {
            var logic = Traverse.Create(crafter).Field<CrafterLogic>("logic").Value;
            if (logic != null)
            {
                logic.TryPickup();
            }

            waitFrames = 0;
            while (Plugin.Services.QueueCoordinator.HasPendingPickupOperations && waitFrames < 60)
            {
                waitFrames++;
                yield return null;
            }

            var retryFrames = 0;
            while (HasCraftedItem(crafter))
            {
                if (Plugin.Services == null || crafter == null)
                {
                    yield break;
                }

                // If queue was manually stopped (Backspace), honor it immediately.
                if (Plugin.Services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
                {
                    Plugin.Services.QueueFeedback.ClearProgress(finishedTechType);
                    Plugin.Services.QueueCoordinator.ResetForQueueEnd();
                    TrySetCraftingMenuLocked(crafter, false);
                    yield break;
                }

                retryFrames++;
                if (logic != null && retryFrames % 30 == 0)
                {
                    logic.TryPickup();
                }

                yield return null;
            }
        }

        if (Plugin.Services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
        {
            Plugin.Services.QueueFeedback.ClearProgress(finishedTechType);
            Plugin.Services.QueueCoordinator.ResetForQueueEnd();
            TrySetCraftingMenuLocked(crafter, false);
            yield break;
        }

        if (!Plugin.Services.Queue.TryPeek(out var next) || next == null)
        {
            Plugin.Services.QueueFeedback.NotifyQueueCompleted();
            Plugin.Services.QueueCoordinator.ResetForQueueEnd();
            TrySetCraftingMenuLocked(crafter, false);
            yield break;
        }

        // Queue continues — clear the progress line of the item that just finished
        Plugin.Services.QueueFeedback.ClearProgress(finishedTechType);

        TrySetCraftingMenuLocked(crafter, true);

        var duration = 3f;
        TechData.GetCraftTime(next.TechType, out duration);

        GhostCrafterCraftMethod?.Invoke(crafter, new object[] { next.TechType, duration });
    }

    private static bool HasCraftedItem(GhostCrafter crafter)
    {
        if (crafter == null)
        {
            return false;
        }

        try
        {
            var result = CrafterHasCraftedItemMethod?.Invoke(crafter, null);
            return result is bool has && has;
        }
        catch
        {
            return false;
        }
    }

    private static void TrySetCraftingMenuLocked(GhostCrafter crafter, bool locked)
    {
        var menu = uGUI.main != null ? uGUI.main.craftingMenu : null;
        if (menu == null)
        {
            return;
        }

        if (!ReferenceEquals(menu.client as object, crafter))
        {
            return;
        }

        try
        {
            CraftingMenuSetLockedMethod?.Invoke(menu, new object[] { locked });
        }
        catch (TargetInvocationException ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(CraftingMenuLockWarningKey, $"Could not synchronize crafting menu lock state: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(CraftingMenuLockWarningKey, $"Could not synchronize crafting menu lock state: {ex.Message}");
        }
    }
}