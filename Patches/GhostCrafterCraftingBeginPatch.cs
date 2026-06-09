using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Logging;
using SubCraftica.Services.Resources;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(GhostCrafter), "OnCraftingBegin")]
internal static class GhostCrafterCraftingBeginPatch
{
    [HarmonyPostfix]
    private static void Postfix(GhostCrafter __instance)
    {
        var instanceType = __instance?.GetType().FullName ?? "null";
        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftingBeginPatch] OnCraftingBegin called for {instanceType}");

        if (__instance == null || Plugin.Services == null)
        {
            SubCrafticaLogger.LogWarning("[GhostCrafterCraftingBeginPatch] Instance or Services null");
            return;
        }

        if (!Plugin.Services.Runtime.TryGetLastTechType(out var techType))
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftingBeginPatch] No last tech type found");
            return;
        }

        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftingBeginPatch] Last tech type: {techType}");

        if (!Plugin.Services.CraftRuntimeState.TryGetRequiredEnergy(techType, out var requiredEnergy))
        {
            requiredEnergy = Plugin.Services.Energy.GetRequiredEnergy(techType, 1);
            SubCrafticaLogger.LogDebug($"[GhostCrafterCraftingBeginPatch] Calculated energy: {requiredEnergy}");
        }

        var powerRelay = Traverse.Create(__instance).Field<PowerRelay>("powerRelay").Value;
        if (Plugin.Services.Energy.TryConsumePlannedEnergy(powerRelay, techType, requiredEnergy))
        {
            SubCrafticaLogger.LogDebug($"[GhostCrafterCraftingBeginPatch] Energy consumed successfully: {requiredEnergy}");
            Plugin.Services.QueueCoordinator.ClearStopQueueContinuationRequested();
            return;
        }

        SubCrafticaLogger.LogWarning($"[GhostCrafterCraftingBeginPatch] Not enough energy: required={requiredEnergy}");
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
        var instanceType = __instance?.GetType().FullName ?? "null";
        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftingEndPatch.Prefix] OnCraftingEnd called for {instanceType}");

        if (__instance == null || Plugin.Services == null)
        {
            SubCrafticaLogger.LogWarning("[GhostCrafterCraftingEndPatch.Prefix] Instance or Services null");
            return;
        }

        // Vanilla only calls logic.TryPickup() in OnCraftingEnd when
        // PlayerIsInRange(closeDistance) || pickupOutOfRange.
        // Force this path so crafted items are pushed immediately even when the player is far.
        __instance.pickupOutOfRange = true;
        SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Prefix] Set pickupOutOfRange = true");
    }

    [HarmonyPostfix]
    private static void Postfix(GhostCrafter __instance)
    {
        SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Postfix] Starting postfix");

        if (__instance == null || Plugin.Services == null)
        {
            SubCrafticaLogger.LogWarning("[GhostCrafterCraftingEndPatch.Postfix] Instance or Services null");
            return;
        }

        // Extra safety: ensure pickup attempt also runs from our side
        // in case another mod short-circuits vanilla flow.
        var logic = Traverse.Create(__instance).Field<CrafterLogic>("logic").Value;
        if (logic != null)
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Postfix] Logic exists, calling logic.TryPickup()");
            logic.TryPickup();
        }
        else
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Postfix] Logic is null - custom fabricator detected");
        }

        var craftingMode = Plugin.Services.Config.CraftingMode.Value;
        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftingEndPatch.Postfix] Crafting mode: {craftingMode}");

        Plugin.Services.PrototypeSubCompat?.TryCleanupAfterCraftingEnd(__instance, Plugin.Services);

        if (craftingMode == ModConfig.CraftingModePerItem
            && Plugin.Services.PrototypeSubCompat != null
            && Plugin.Services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Postfix] Prototype per-item client detected, continuation delegated to compat patch");
            return;
        }

        if (craftingMode != ModConfig.CraftingModePerItem)
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Postfix] Not per-item mode, skipping queue continuation");
            return;
        }

        SubCrafticaLogger.LogDebug("[GhostCrafterCraftingEndPatch.Postfix] Starting per-item queue end coroutine");
        __instance.StartCoroutine(HandlePerItemQueueEnd(__instance));
    }

    private static IEnumerator HandlePerItemQueueEnd(GhostCrafter crafter)
    {
        // Consume the techType that just finished — must read it before first yield
        // because the Finalizer in GhostCrafterCraftPatch clears Runtime.lastTechType
        // before OnCraftingEnd fires.
        var finishedTechType = Plugin.Services.Runtime.ConsumeLastPerItemFinished();
        SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Consumed finished tech type: {finishedTechType}");

        yield return null;

        if (crafter == null || Plugin.Services == null)
        {
            SubCrafticaLogger.LogWarning("[HandlePerItemQueueEnd] Crafter or Services null after first yield");
            yield break;
        }

        var waitFrames = 0;
        while (Plugin.Services.Synchronization.IsCraftInProgress && waitFrames < 5)
        {
            waitFrames++;
            yield return null;
        }
        SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Waited {waitFrames} frames for craft sync");

        waitFrames = 0;
        while (Plugin.Services.QueueCoordinator.HasPendingPickupOperations && waitFrames < 30)
        {
            waitFrames++;
            yield return null;
        }
        SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Waited {waitFrames} frames for pickup ops");

        var logic = Traverse.Create(crafter).Field<CrafterLogic>("logic").Value;
        if (logic == null)
        {
            SubCrafticaLogger.LogDebug("[HandlePerItemQueueEnd] Logic is null - custom fabricator detected, skipping pickup loop");
        }
        else
        {
            // Resolve crafted output before queue continuation.
            var hasCrafted = HasCraftedItem(crafter);
            SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] HasCraftedItem check: {hasCrafted}");

            if (hasCrafted)
            {
                SubCrafticaLogger.LogDebug("[HandlePerItemQueueEnd] Calling logic.TryPickup()");
                logic.TryPickup();

                waitFrames = 0;
                while (Plugin.Services.QueueCoordinator.HasPendingPickupOperations && waitFrames < 60)
                {
                    waitFrames++;
                    yield return null;
                }
                SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Waited {waitFrames} frames for pickup ops (2nd)");

                var retryFrames = 0;
                while (HasCraftedItem(crafter))
                {
                    if (Plugin.Services == null || crafter == null)
                    {
                        SubCrafticaLogger.LogWarning("[HandlePerItemQueueEnd] Lost services/crafter during retry loop");
                        yield break;
                    }

                    // If queue was manually stopped (Backspace), honor it immediately.
                    if (Plugin.Services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
                    {
                        SubCrafticaLogger.LogDebug("[HandlePerItemQueueEnd] Queue continuation stopped (manual)");
                        Plugin.Services.QueueFeedback.ClearProgress(finishedTechType);
                        Plugin.Services.QueueCoordinator.ResetForQueueEnd();
                        TrySetCraftingMenuLocked(crafter, false);
                        yield break;
                    }

                    retryFrames++;
                    if (retryFrames % 30 == 0)
                    {
                        SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Retry TryPickup (frame {retryFrames})");
                        logic.TryPickup();
                    }

                    yield return null;
                }
                SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Item pickup complete after {retryFrames} retries");
            }
            else
            {
                SubCrafticaLogger.LogDebug("[HandlePerItemQueueEnd] No crafted item detected, skipping pickup loop");
            }
        }

        if (Plugin.Services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
        {
            SubCrafticaLogger.LogDebug("[HandlePerItemQueueEnd] Queue continuation stopped (post-pickup)");
            Plugin.Services.QueueFeedback.ClearProgress(finishedTechType);
            Plugin.Services.QueueCoordinator.ResetForQueueEnd();
            TrySetCraftingMenuLocked(crafter, false);
            yield break;
        }

        if (!Plugin.Services.Queue.TryPeek(out var next) || next == null)
        {
            SubCrafticaLogger.LogDebug("[HandlePerItemQueueEnd] Queue empty, notifying completion");
            Plugin.Services.QueueFeedback.NotifyQueueCompleted();
            Plugin.Services.QueueCoordinator.ResetForQueueEnd();
            TrySetCraftingMenuLocked(crafter, false);
            yield break;
        }

        // Queue continues — clear the progress line of the item that just finished
        SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Continuing queue with next: {next.TechType}");
        Plugin.Services.QueueFeedback.ClearProgress(finishedTechType);
        TrySetCraftingMenuLocked(crafter, true);

        var duration = 3f;
        TechData.GetCraftTime(next.TechType, out duration);

        SubCrafticaLogger.LogDebug($"[HandlePerItemQueueEnd] Invoking next craft: {next.TechType}, duration={duration}");
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
            // Try using reflection on Crafter.HasCraftedItem first (for vanilla GhostCrafter)
            var result = CrafterHasCraftedItemMethod?.Invoke(crafter, null);
            if (result is bool has && has)
            {
                SubCrafticaLogger.LogDebug("[HasCraftedItem] Reflection method returned True");
                return true;
            }
        }
        catch
        {
            // Silently continue to fallback
        }

        // Fallback for custom fabricators without CrafterLogic:
        // For AlienFabricator and similar, we assume an item was crafted if we got here
        // because OnCraftingEnd is only called after crafting completes.
        // The only way HasCraftedItem could be false legitimately is if vanilla GhostCrafter
        // had no output recipe, which is rare.
        SubCrafticaLogger.LogDebug("[HasCraftedItem] No reflection result, assuming custom fabricator produced output");
        return true;  // Assume crafted for custom fabricators
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
