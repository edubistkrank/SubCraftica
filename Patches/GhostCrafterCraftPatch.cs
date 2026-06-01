using System;
using HarmonyLib;
using SubCraftica.Services.Composition;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Crafting;
using SubCraftica.Services.Localization;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) })]
internal static class GhostCrafterCraftPatch
{
    internal static ModServices Services { get; set; }

    [ThreadStatic]
    private static bool _didEnterCraft = false;

    [HarmonyPrefix]
    private static bool Prefix(GhostCrafter __instance, TechType techType)
    {
        _didEnterCraft = false;

        if (Services == null)
        {
            return true;
        }

        if (!Services.Synchronization.TryEnterCraft())
        {
            ErrorMessage.AddWarning(ModText.Get(ModText.CraftAlreadyRunning));
            return false;
        }

        _didEnterCraft = true;
        Services.Runtime.SetLastTechType(techType);

        if (!Services.Queue.TryDequeueForTechType(techType, out var request))
        {
            // No queued request: vanilla single craft, just track energy
            Services.CraftRuntimeState.SetRequiredEnergy(techType, Services.Energy.GetRequiredEnergy(techType, 1));
            Services.RecipeOverride.Restore(techType);
            return true;
        }

        var craftingMode = Services.Config.CraftingMode.Value;

        return craftingMode == ModConfig.CraftingModePerItem
            ? HandlePerItemCraft(__instance, techType, request)
            : HandleBatchOrInstantCraft(__instance, techType, request, craftingMode);
    }

    // --- Per-item mode: always crafts 1, requeues remainder ---

    private static bool HandlePerItemCraft(GhostCrafter instance, TechType techType, CraftingRequest request)
    {
        var totalAmount = request.TotalAmount;
        var remainder = request.Amount - 1;

        if (remainder > 0)
        {
            Services.Queue.TryEnqueue(new CraftingRequest(techType, remainder, totalAmount), Services.Config.MaxQueueSize.Value);
        }

        var crafted = totalAmount - remainder;
        Services.QueueFeedback.NotifyCraftProgress(techType, crafted, totalAmount);

        var powerRelay = Traverse.Create(instance).Field<PowerRelay>("powerRelay").Value;
        var plan = Services.RecipePlanner.BuildPlan(techType, 1);
        if (!plan.Success)
        {
            HandleMissingIngredientsFailure(techType, ModConfig.CraftingModePerItem);
            return false;
        }

        var requiredEnergy = Services.Energy.GetRequiredEnergy(techType, plan.Crafted);
        if (!Services.Energy.HasEnoughEnergy(powerRelay, techType, requiredEnergy))
        {
            HandleNotEnoughPowerFailure(techType, ModConfig.CraftingModePerItem);
            return false;
        }

        Services.RecipeOverride.ApplyAmountOverride(techType, 1);
        Services.CraftRuntimeState.SetRequiredEnergy(techType, requiredEnergy);
        return true;
    }

    // --- Batch / instant mode: crafts as many as energy allows ---

    private static bool HandleBatchOrInstantCraft(GhostCrafter instance, TechType techType, CraftingRequest request, int craftingMode)
    {
        var requestedAmount = request.Amount;
        var requestTotalAmount = request.TotalAmount;
        var powerRelay = Traverse.Create(instance).Field<PowerRelay>("powerRelay").Value;

        var craftAmount = requestedAmount;
        float requiredEnergy = 0f;
        CraftPlanResult plan = null;
        var failedOnIngredients = false;

        while (craftAmount > 0)
        {
            plan = Services.RecipePlanner.BuildPlan(techType, craftAmount);
            if (!plan.Success)
            {
                failedOnIngredients = true;
                break;
            }

            requiredEnergy = Services.Energy.GetRequiredEnergy(techType, plan.Crafted);
            if (Services.Energy.HasEnoughEnergy(powerRelay, techType, requiredEnergy))
            {
                break;
            }

            craftAmount--;
        }

        if (craftAmount <= 0 || plan == null || !plan.Success)
        {
            if (failedOnIngredients)
            {
                HandleMissingIngredientsFailure(techType, craftingMode);
            }
            else
            {
                HandleNotEnoughPowerFailure(techType, craftingMode);
            }

            return false;
        }

        if (craftAmount < requestedAmount)
        {
            // Partial craft due to energy: requeue what could not be crafted
            ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
            var remainder = requestedAmount - craftAmount;
            Services.Queue.TryEnqueue(new CraftingRequest(techType, remainder, requestTotalAmount), Services.Config.MaxQueueSize.Value);
        }

        // Mark queue-completed notification if this is the last item in queue
        if (Services.Queue.Count == 0)
        {
            Services.QueueCoordinator.SetShouldNotifyQueueCompleted();
        }

        Services.RecipeOverride.ApplyAmountOverride(techType, craftAmount);
        Services.CraftRuntimeState.SetRequiredEnergy(techType, requiredEnergy);
        return true;
    }

    // --- Failure handlers ---

    /// <summary>Emitted when the recipe planner cannot fulfill ingredients mid-queue.</summary>
    private static void HandleMissingIngredientsFailure(TechType techType, int craftingMode)
    {
        if (craftingMode == ModConfig.CraftingModePerItem)
        {
            Services.QueueCoordinator.RequestStopQueueContinuation(Services.Queue);
        }

        Services.CraftRuntimeState.Clear(techType);
        Services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(Language.main.Get("DontHaveNeededIngredients"));
    }

    /// <summary>Emitted when the power relay does not have enough energy to start the craft.</summary>
    private static void HandleNotEnoughPowerFailure(TechType techType, int craftingMode)
    {
        if (craftingMode == ModConfig.CraftingModePerItem)
        {
            Services.QueueCoordinator.RequestStopQueueContinuation(Services.Queue);
        }

        Services.CraftRuntimeState.Clear(techType);
        Services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, TechType techType)
    {
        Services?.RecipeOverride.Restore(techType);
        Services?.Runtime.Clear(techType);
        Services?.CraftRuntimeState.Clear(techType);

        if (_didEnterCraft)
        {
            Services?.Synchronization.ExitCraft();
            _didEnterCraft = false;
        }

        return __exception;
    }
}

[HarmonyPatch(typeof(GhostCrafter), "OnCraftingEnd")]
internal static class GhostCrafterBatchInstantCompletionPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        var services = Plugin.Services;
        if (services == null)
        {
            return;
        }

        if (services.Config.CraftingMode.Value == ModConfig.CraftingModePerItem)
        {
            return;
        }

        if (services.QueueCoordinator.ConsumeShouldNotifyQueueCompleted())
        {
            services.QueueFeedback.NotifyQueueCompleted();
        }
    }
}