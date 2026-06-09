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

    private static int queuedCraftsInFlight;

    [HarmonyPrefix]
    private static bool Prefix(GhostCrafter __instance, TechType techType)
    {
        var services = GhostCrafterCraftPatch.Services;
        if (services == null)
        {
            return true;
        }

        if (services.PrototypeSubCompat == null || !services.PrototypeSubCompat.IsPrototypeFabricator(__instance))
        {
            return true;
        }

        // In batch/instant let the main GhostCrafterCraftPatch own the full flow.
        // This compat prefix is only needed for per-item continuation behavior.
        if (services.Config.CraftingMode.Value != ModConfig.CraftingModePerItem)
        {
            return true;
        }

        services.Runtime.SetLastTechType(techType);

        if (!services.Queue.TryDequeueForTechType(techType, out var request))
        {
            services.CraftRuntimeState.SetRequiredEnergy(techType, services.Energy.GetRequiredEnergy(techType, 1));
            services.RecipeOverride.Restore(techType);
            return true;
        }

        var allowed = HandlePrototypePerItemCraft(__instance, techType, request, services);
        if (allowed)
        {
            queuedCraftsInFlight++;
        }

        return allowed;
    }

    private static bool HandlePrototypePerItemCraft(GhostCrafter instance, TechType techType, CraftingRequest request, ModServices services)
    {
        var totalAmount = request.TotalAmount;
        var remainder = request.Amount - 1;

        if (remainder > 0)
        {
            services.Queue.TryEnqueueFront(new CraftingRequest(techType, remainder, totalAmount), services.Config.MaxQueueSize.Value);
        }

        var crafted = totalAmount - remainder;
        services.QueueFeedback.NotifyCraftProgress(techType, crafted, totalAmount);
        services.Runtime.SetLastPerItemFinished(techType);

        var isDefabRecycle = services.DefabricatorCompat != null
                             && services.DefabricatorCompat.IsDefabricationActiveFor(techType);

        if (services.Config.CreativeMode.Value)
        {
            services.RecipeOverride.ApplyAmountOverride(techType, 1);
            services.CraftRuntimeState.SetRequiredEnergy(techType, 0f);
            return true;
        }

        if (isDefabRecycle)
        {
            services.RecipeOverride.Restore(techType);
            services.CraftRuntimeState.SetRequiredEnergy(techType, services.Energy.GetRequiredEnergy(techType, 1));
            return true;
        }

        var powerRelay = Traverse.Create(instance).Field<PowerRelay>("powerRelay").Value;
        var plan = services.RecipePlanner.BuildPlan(techType, 1);
        if (!plan.Success)
        {
            HandleMissingIngredientsFailure(techType, services);
            return false;
        }

        var requiredEnergy = services.Energy.GetRequiredEnergy(techType, plan.Crafted);
        if (!services.Energy.HasEnoughEnergy(powerRelay, techType, requiredEnergy))
        {
            HandleNotEnoughPowerFailure(techType, services);
            return false;
        }

        services.RecipeOverride.ApplyAmountOverride(techType, 1);
        services.CraftRuntimeState.SetRequiredEnergy(techType, requiredEnergy);
        return true;
    }

    private static void HandleMissingIngredientsFailure(TechType techType, ModServices services)
    {
        services.QueueCoordinator.RequestStopQueueContinuation(services.Queue);
        services.QueueFeedback.ClearAllProgress();
        services.CraftRuntimeState.Clear(techType);
        services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
    }

    private static void HandleNotEnoughPowerFailure(TechType techType, ModServices services)
    {
        services.QueueCoordinator.RequestStopQueueContinuation(services.Queue);
        services.QueueFeedback.ClearAllProgress();
        services.CraftRuntimeState.Clear(techType);
        services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, TechType techType)
    {
        var services = GhostCrafterCraftPatch.Services;
        if (services != null && services.PrototypeSubCompat != null && services.PrototypeSubCompat.IsPrototypeFabricatorClientActive())
        {
            services.RecipeOverride.Restore(techType);
            services.Runtime.Clear(techType);
            services.CraftRuntimeState.Clear(techType);
        }

        return __exception;
    }

    private static IEnumerator ContinueQueuedCraft(GhostCrafter crafter, ModServices services)
    {
        var finishedTechType = services.Runtime.ConsumeLastPerItemFinished();

        yield return null;

        var waitFrames = 0;
        while (services.PrototypeSubCompat != null && services.PrototypeSubCompat.IsAlienFabricatorCrafting(crafter) && waitFrames < 600)
        {
            waitFrames++;
            yield return null;
        }

        if (services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
        {
            services.QueueFeedback.ClearProgress(finishedTechType);
            services.QueueCoordinator.ResetForQueueEnd();
            yield break;
        }

        if (!services.Queue.TryPeek(out var next) || next == null)
        {
            services.QueueFeedback.ClearProgress(finishedTechType);
            services.QueueFeedback.NotifyQueueCompleted();
            services.QueueCoordinator.ResetForQueueEnd();
            yield break;
        }

        services.QueueFeedback.ClearProgress(finishedTechType);

        var duration = 3f;
        TechData.GetCraftTime(next.TechType, out duration);
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
                return;
            }

            if (queuedCraftsInFlight <= 0)
            {
                return;
            }

            queuedCraftsInFlight--;
            __instance.StartCoroutine(ContinueQueuedCraft(__instance, services));
        }
    }
}
