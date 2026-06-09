using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Composition;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Crafting;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Logging;

namespace SubCraftica.Patches.Compat;

[HarmonyPatch(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) })]
[HarmonyPriority(Priority.First)]
internal static class PrototypeSubGhostCrafterCraftCompatPatch
{
    private static readonly MethodInfo GhostCrafterCraftMethod =
        AccessTools.Method(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) });

    [HarmonyPrefix]
    private static bool Prefix(GhostCrafter __instance, TechType techType)
    {
        PrototypeCompatDebugLogger.Debug($"Craft.Prefix enter instanceType={__instance?.GetType().FullName ?? "null"} techType={techType}");
        var services = GhostCrafterCraftPatch.Services;
        if (services == null)
        {
            PrototypeCompatDebugLogger.Warn("Craft.Prefix services null -> allow vanilla");
            return true;
        }

        if (services.PrototypeSubCompat == null || !services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
        {
            PrototypeCompatDebugLogger.Debug("Craft.Prefix non-prototype fabricator -> allow vanilla");
            return true;
        }

        // In batch/instant let the main GhostCrafterCraftPatch own the full flow.
        // This compat prefix is only needed for per-item continuation behavior.
        if (services.Config.CraftingMode.Value != ModConfig.CraftingModePerItem)
        {
            PrototypeCompatDebugLogger.Debug($"Craft.Prefix mode={services.Config.CraftingMode.Value} not per-item -> main patch handles flow");
            return true;
        }

        services.Runtime.SetLastTechType(techType);

        if (!services.Queue.TryDequeueForTechType(techType, out var request))
        {
            PrototypeCompatDebugLogger.Debug("Craft.Prefix no matching queued request -> vanilla single craft");
            services.CraftRuntimeState.SetRequiredEnergy(techType, services.Energy.GetRequiredEnergy(techType, 1));
            services.RecipeOverride.Restore(techType);
            return true;
        }

        PrototypeCompatDebugLogger.Info($"Craft.Prefix dequeued request amount={request.Amount} totalAmount={request.TotalAmount}");

        var allowed = HandlePrototypePerItemCraft(__instance, techType, request, services);

        if (allowed)
        {
            PrototypeCompatDebugLogger.Debug("Craft.Prefix allowed");
        }
        else
        {
            PrototypeCompatDebugLogger.Warn("Craft.Prefix denied craft for current request");
        }

        return allowed;
    }

    private static bool HandlePrototypePerItemCraft(GhostCrafter instance, TechType techType, CraftingRequest request, ModServices services)
    {
        var totalAmount = request.TotalAmount;
        var remainder = request.Amount - 1;
        PrototypeCompatDebugLogger.Debug($"HandlePrototypePerItemCraft start techType={techType} total={totalAmount} remainder={remainder}");

        if (remainder > 0)
        {
            services.Queue.TryEnqueueFront(new CraftingRequest(techType, remainder, totalAmount), services.Config.MaxQueueSize.Value);
            PrototypeCompatDebugLogger.Debug($"HandlePrototypePerItemCraft requeued remainder={remainder}");
        }

        var crafted = totalAmount - remainder;
        services.QueueFeedback.NotifyCraftProgress(techType, crafted, totalAmount);
        services.Runtime.SetLastPerItemFinished(techType);

        var isDefabRecycle = services.DefabricatorCompat != null
                             && services.DefabricatorCompat.IsDefabricationActiveFor(techType);

        if (services.Config.CreativeMode.Value)
        {
            PrototypeCompatDebugLogger.Info("HandlePrototypePerItemCraft creative mode path");
            services.RecipeOverride.ApplyAmountOverride(techType, 1);
            services.CraftRuntimeState.SetRequiredEnergy(techType, 0f);
            return true;
        }

        if (isDefabRecycle)
        {
            PrototypeCompatDebugLogger.Info("HandlePrototypePerItemCraft defabricator recycle path");
            services.RecipeOverride.Restore(techType);
            services.CraftRuntimeState.SetRequiredEnergy(techType, services.Energy.GetRequiredEnergy(techType, 1));
            return true;
        }

        var powerRelay = Traverse.Create(instance).Field<PowerRelay>("powerRelay").Value;
        var plan = services.RecipePlanner.BuildPlan(techType, 1);
        if (!plan.Success)
        {
            PrototypeCompatDebugLogger.Warn("HandlePrototypePerItemCraft plan failed");
            HandleMissingIngredientsFailure(techType, services);
            return false;
        }

        var requiredEnergy = services.Energy.GetRequiredEnergy(techType, plan.Crafted);
        if (!services.Energy.HasEnoughEnergy(powerRelay, techType, requiredEnergy))
        {
            PrototypeCompatDebugLogger.Warn($"HandlePrototypePerItemCraft not enough energy required={requiredEnergy}");
            HandleNotEnoughPowerFailure(techType, services);
            return false;
        }

        PrototypeCompatDebugLogger.Debug($"HandlePrototypePerItemCraft success requiredEnergy={requiredEnergy}");

        services.RecipeOverride.ApplyAmountOverride(techType, 1);
        services.CraftRuntimeState.SetRequiredEnergy(techType, requiredEnergy);
        return true;
    }

    private static void HandleMissingIngredientsFailure(TechType techType, ModServices services)
    {
        PrototypeCompatDebugLogger.Warn($"HandleMissingIngredientsFailure techType={techType}");
        services.QueueCoordinator.RequestStopQueueContinuation(services.Queue);
        services.QueueFeedback.ClearAllProgress();
        services.CraftRuntimeState.Clear(techType);
        services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
    }

    private static void HandleNotEnoughPowerFailure(TechType techType, ModServices services)
    {
        PrototypeCompatDebugLogger.Warn($"HandleNotEnoughPowerFailure techType={techType}");
        services.QueueCoordinator.RequestStopQueueContinuation(services.Queue);
        services.QueueFeedback.ClearAllProgress();
        services.CraftRuntimeState.Clear(techType);
        services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, TechType techType)
    {
        if (__exception != null)
        {
            PrototypeCompatDebugLogger.Error(__exception, $"Craft.Finalizer exception techType={techType}");
        }

        var services = GhostCrafterCraftPatch.Services;
        if (services != null && services.PrototypeSubCompat != null && services.PrototypeSubCompat.IsPrototypeFabricatorClientActive())
        {
            PrototypeCompatDebugLogger.Debug($"Craft.Finalizer restoring runtime state for {techType}");
            services.RecipeOverride.Restore(techType);
            services.Runtime.Clear(techType);
            services.CraftRuntimeState.Clear(techType);
        }

        return __exception;
    }

    private static IEnumerator ContinueQueuedCraft(GhostCrafter crafter, ModServices services)
    {
        var finishedTechType = services.Runtime.ConsumeLastPerItemFinished();
        PrototypeCompatDebugLogger.Debug($"ContinueQueuedCraft start finishedTechType={finishedTechType}");

        yield return null;

        var waitFrames = 0;
        while (services.PrototypeSubCompat != null && services.PrototypeSubCompat.IsAlienFabricatorCrafting(crafter) && waitFrames < 600)
        {
            waitFrames++;
            yield return null;
        }
        PrototypeCompatDebugLogger.Debug($"ContinueQueuedCraft waitedFrames={waitFrames}");

        if (services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
        {
            PrototypeCompatDebugLogger.Info("ContinueQueuedCraft stop requested -> ending queue");
            if (finishedTechType != TechType.None)
            {
                services.QueueFeedback.ClearProgress(finishedTechType);
            }
            services.QueueCoordinator.ResetForQueueEnd();
            yield break;
        }

        if (!services.Queue.TryPeek(out var next) || next == null)
        {
            PrototypeCompatDebugLogger.Info("ContinueQueuedCraft queue empty");
            if (finishedTechType != TechType.None)
            {
                services.QueueFeedback.ClearProgress(finishedTechType);
            }

            if (services.QueueCoordinator.ConsumeShouldNotifyQueueCompleted())
            {
                PrototypeCompatDebugLogger.Info("ContinueQueuedCraft notifying queue completed");
                services.QueueFeedback.NotifyQueueCompleted();
            }

            services.QueueCoordinator.ResetForQueueEnd();
            yield break;
        }

        if (finishedTechType != TechType.None)
        {
            services.QueueFeedback.ClearProgress(finishedTechType);
        }

        var duration = 3f;
        TechData.GetCraftTime(next.TechType, out duration);
        PrototypeCompatDebugLogger.Info($"ContinueQueuedCraft invoking next techType={next.TechType} duration={duration}");
        GhostCrafterCraftMethod?.Invoke(crafter, new object[] { next.TechType, duration });
    }

    [HarmonyPatch(typeof(GhostCrafter), "OnCraftingEnd")]
    [HarmonyPriority(Priority.Last)]
    private static class PrototypeSubGhostCrafterCraftingEndCompatPatch
    {
        [HarmonyPrefix]
        private static void Prefix(GhostCrafter __instance)
        {
            var services = Plugin.Services;
            if (__instance == null || services?.PrototypeSubCompat == null || !services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
            {
                return;
            }

            PrototypeCompatDebugLogger.Debug("OnCraftingEnd.Prefix setting pickupOutOfRange=true for prototype");
            __instance.pickupOutOfRange = true;
        }

        [HarmonyPostfix]
        private static void Postfix(GhostCrafter __instance)
        {
            var services = Plugin.Services;
            if (__instance == null || services?.PrototypeSubCompat == null || !services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
            {
                return;
            }

            if (services.Config.CraftingMode.Value == ModConfig.CraftingModePerItem)
            {
                PrototypeCompatDebugLogger.Debug("OnCraftingEnd.Postfix per-item mode -> scheduling compat continuation");
                __instance.StartCoroutine(ContinueQueuedCraft(__instance, services));
                return;
            }

            PrototypeCompatDebugLogger.Debug("OnCraftingEnd.Postfix scheduling non-per-item continuation check");
            __instance.StartCoroutine(ContinueQueuedCraft(__instance, services));
        }
    }
}
