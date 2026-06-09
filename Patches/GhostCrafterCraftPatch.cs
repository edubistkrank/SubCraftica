using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Composition;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Crafting;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Logging;

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

        var instanceType = __instance?.GetType().FullName ?? "null";
        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftPatch.Prefix] Called for instance type={instanceType}, techType={techType}");

        if (Services == null)
        {
            SubCrafticaLogger.LogWarning("[GhostCrafterCraftPatch.Prefix] Services is null, allowing vanilla craft");
            return true;
        }

        if (Services.PrototypeSubCompat != null
            && Services.PrototypeSubCompat.ShouldBypassMainCraftPrefix(__instance, Services.Config.CraftingMode.Value))
        {
            return true;
        }


        if (!Services.Synchronization.TryEnterCraft())
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftPatch.Prefix] Synchronization failed - craft already running");
            ErrorMessage.AddWarning(ModText.Get(ModText.CraftAlreadyRunning));
            return false;
        }
        _didEnterCraft = true;

        Services.Runtime.SetLastTechType(techType);
        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftPatch.Prefix] Entered craft handling for techType={techType}");

        if (!Services.Queue.TryDequeueForTechType(techType, out var request))
        {
            // No queued request: vanilla single craft, just track energy
            SubCrafticaLogger.LogDebug($"[GhostCrafterCraftPatch.Prefix] No queued request - vanilla single craft");
            Services.CraftRuntimeState.SetRequiredEnergy(techType, Services.Energy.GetRequiredEnergy(techType, 1));
            Services.RecipeOverride.Restore(techType);
            return true;
        }

        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftPatch.Prefix] Dequeued request: amount={request.Amount}, totalAmount={request.TotalAmount}");
        var craftingMode = Services.Config.CraftingMode.Value;

        var usePerItemFlow = craftingMode == ModConfig.CraftingModePerItem;

        var result = usePerItemFlow
            ? HandlePerItemCraft(__instance, techType, request)
            : HandleBatchOrInstantCraft(__instance, techType, request, craftingMode);

        SubCrafticaLogger.LogDebug($"[GhostCrafterCraftPatch.Prefix] Craft handling returned {result}");
        return result;
    }

    // --- Per-item mode: always crafts 1, requeues remainder ---

    private static bool HandlePerItemCraft(GhostCrafter instance, TechType techType, CraftingRequest request)
    {
        SubCrafticaLogger.LogDebug($"[HandlePerItemCraft] Starting per-item craft: amount={request.Amount}, totalAmount={request.TotalAmount}");

        var totalAmount = request.TotalAmount;
        var remainder = request.Amount - 1;

        if (remainder > 0)
        {
            SubCrafticaLogger.LogDebug($"[HandlePerItemCraft] Requeuing remainder: {remainder}");
            Services.Queue.TryEnqueueFront(new CraftingRequest(techType, remainder, totalAmount), Services.Config.MaxQueueSize.Value);
        }

        var crafted = totalAmount - remainder;
        SubCrafticaLogger.LogDebug($"[HandlePerItemCraft] Notifying progress: crafted={crafted}/{totalAmount}");
        Services.QueueFeedback.NotifyCraftProgress(techType, crafted, totalAmount);
        Services.Runtime.SetLastPerItemFinished(techType);

        var isDefabRecycle = Services.DefabricatorCompat != null
                             && Services.DefabricatorCompat.IsDefabricationActiveFor(techType);

        if (Services.Config.CreativeMode.Value)
        {
            SubCrafticaLogger.LogDebug("[HandlePerItemCraft] Creative mode enabled - zero energy");
            Services.RecipeOverride.ApplyAmountOverride(techType, 1);
            Services.CraftRuntimeState.SetRequiredEnergy(techType, 0f);
            return true;
        }

        if (isDefabRecycle)
        {
            SubCrafticaLogger.LogDebug("[HandlePerItemCraft] Defab recycle active");
            Services.RecipeOverride.Restore(techType);
            Services.CraftRuntimeState.SetRequiredEnergy(techType, Services.Energy.GetRequiredEnergy(techType, 1));
            return true;
        }

        var powerRelay = Traverse.Create(instance).Field<PowerRelay>("powerRelay").Value;
        var plan = Services.RecipePlanner.BuildPlan(techType, 1);
        if (!plan.Success)
        {
            SubCrafticaLogger.LogWarning($"[HandlePerItemCraft] Plan failed for techType={techType}");
            HandleMissingIngredientsFailure(techType, ModConfig.CraftingModePerItem);
            return false;
        }

        var requiredEnergy = Services.Energy.GetRequiredEnergy(techType, plan.Crafted);
        if (!Services.Energy.HasEnoughEnergy(powerRelay, techType, requiredEnergy))
        {
            SubCrafticaLogger.LogWarning($"[HandlePerItemCraft] Not enough energy: required={requiredEnergy}");
            HandleNotEnoughPowerFailure(techType, ModConfig.CraftingModePerItem);
            return false;
        }

        SubCrafticaLogger.LogDebug($"[HandlePerItemCraft] Success - energy={requiredEnergy}");
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

        var isDefabRecycle = Services.DefabricatorCompat != null
                             && Services.DefabricatorCompat.IsDefabricationActiveFor(techType);

        if (isDefabRecycle && !Services.Config.CreativeMode.Value)
        {
            if (!Services.DefabricatorCompat.CanRecycleAmount(techType, requestedAmount))
            {
                HandleMissingIngredientsFailure(techType, craftingMode);
                return false;
            }

            Services.RecipeOverride.Restore(techType);
            var recycleEnergy = Services.Energy.GetRequiredEnergy(techType, requestedAmount);
            if (!Services.Energy.HasEnoughEnergy(powerRelay, techType, recycleEnergy))
            {
                HandleNotEnoughPowerFailure(techType, craftingMode);
                return false;
            }

            if (Services.Queue.Count == 0)
            {
                Services.QueueCoordinator.SetShouldNotifyQueueCompleted();
            }

            Services.CraftRuntimeState.SetRequiredEnergy(techType, recycleEnergy);
            return true;
        }

        var craftAmount = requestedAmount;
        float requiredEnergy = 0f;
        CraftPlanResult plan = null;
        var failedOnIngredients = false;

        while (craftAmount > 0)
        {
            if (Services.Config.CreativeMode.Value)
            {
                requiredEnergy = 0f;
                break;
            }

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

        if (craftAmount <= 0 || (!Services.Config.CreativeMode.Value && (plan == null || !plan.Success)))
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

        if (!Services.Config.CreativeMode.Value && craftAmount < requestedAmount)
        {
            // Partial craft due to energy: requeue what could not be crafted
            ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
            var remainder = requestedAmount - craftAmount;
            Services.Queue.TryEnqueueFront(new CraftingRequest(techType, remainder, requestTotalAmount), Services.Config.MaxQueueSize.Value);
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

        Services.QueueFeedback.ClearAllProgress();
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

        Services.QueueFeedback.ClearAllProgress();
        Services.CraftRuntimeState.Clear(techType);
        Services.RecipeOverride.Restore(techType);
        ErrorMessage.AddWarning(ModText.Get(ModText.WarningNotEnoughPower));
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, GhostCrafter __instance, TechType techType)
    {
        if (__exception != null)
        {
            SubCrafticaLogger.LogError($"[GhostCrafterCraftPatch.Finalizer] Exception: {__exception.GetType().Name} - {__exception.Message}\n{__exception.StackTrace}");
        }
        else
        {
            SubCrafticaLogger.LogDebug($"[GhostCrafterCraftPatch.Finalizer] Success for techType={techType}");
        }

        var shouldDeferCleanupForPrototype = Services != null
                                             && Services.PrototypeSubCompat != null
                                             && Services.PrototypeSubCompat.ShouldDeferCraftCleanup(__instance, Services.Config.CraftingMode.Value);

        if (!shouldDeferCleanupForPrototype)
        {
            Services?.RecipeOverride.Restore(techType);
            Services?.Runtime.Clear(techType);
            Services?.CraftRuntimeState.Clear(techType);
        }

        if (_didEnterCraft)
        {
            SubCrafticaLogger.LogDebug("[GhostCrafterCraftPatch.Finalizer] Exiting craft synchronization");
            Services?.Synchronization.ExitCraft();
            _didEnterCraft = false;
        }

        return __exception;
    }

    }

[HarmonyPatch(typeof(GhostCrafter), "OnCraftingEnd")]
internal static class GhostCrafterBatchInstantCompletionPatch
{
    private static readonly MethodInfo GhostCrafterCraftMethod = AccessTools.Method(typeof(GhostCrafter), "Craft", new[] { typeof(TechType), typeof(float) });

    private static IEnumerator ContinueBatchQueue(GhostCrafter crafter, ModServices services)
    {
        yield return null;

        var waitFrames = 0;
        while (services.QueueCoordinator.HasPendingPickupOperations && waitFrames < 60)
        {
            waitFrames++;
            yield return null;
        }

        if (services.QueueCoordinator.ConsumeStopQueueContinuationRequested())
        {
            services.QueueFeedback.ClearAllProgress();
            services.QueueCoordinator.ResetForQueueEnd();
            yield break;
        }

        if (!services.Queue.TryPeek(out var next) || next == null)
        {
            if (services.QueueCoordinator.ConsumeShouldNotifyQueueCompleted())
            {
                services.QueueFeedback.NotifyQueueCompleted();
            }

            services.QueueCoordinator.ResetForQueueEnd();
            yield break;
        }

        var duration = 3f;
        TechData.GetCraftTime(next.TechType, out duration);
        GhostCrafterCraftMethod?.Invoke(crafter, new object[] { next.TechType, duration });
    }

    [HarmonyPostfix]
    private static void Postfix(GhostCrafter __instance)
    {
        var services = Plugin.Services;
        if (services == null || __instance == null)
        {
            return;
        }

        var prototypeCompat = services.PrototypeSubCompat;
        if (prototypeCompat != null && prototypeCompat.IsPrototypeFabricator(__instance))
        {
            return;
        }

        var craftingMode = services.Config.CraftingMode.Value;
        if (craftingMode == ModConfig.CraftingModePerItem)
        {
            return;
        }

        if (craftingMode == ModConfig.CraftingModeBatch)
        {
            __instance.StartCoroutine(ContinueBatchQueue(__instance, services));
            return;
        }

        if (services.QueueCoordinator.ConsumeShouldNotifyQueueCompleted())
        {
            services.QueueFeedback.NotifyQueueCompleted();
            services.QueueCoordinator.ResetForQueueEnd();
        }
    }
}