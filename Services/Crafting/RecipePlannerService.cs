using System;
using System.Collections.Generic;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Resources;

namespace SubCraftica.Services.Crafting;

internal sealed class RecipePlannerService
{
    private const int MaxPlannerDepth = 10;

    private readonly ModConfig config;
    private readonly StackingCountService stackingCount;
    private readonly NearbyStorageService nearbyStorage;

    public RecipePlannerService(ModConfig config, StackingCountService stackingCount, NearbyStorageService nearbyStorage)
    {
        this.config = config;
        this.stackingCount = stackingCount;
        this.nearbyStorage = nearbyStorage;
    }

    public CraftPlanResult BuildRequestPlan(TechType targetTechType, int requestAmount)
    {
        return BuildPlan(targetTechType, GetRequestedOutputAmount(targetTechType, requestAmount));
    }

    public int GetRequestedOutputAmount(TechType targetTechType, int requestAmount)
    {
        if (targetTechType == TechType.None || requestAmount <= 0)
        {
            return 0;
        }

        return requestAmount * GetEffectiveOutputPerCraft(targetTechType);
    }

    public CraftPlanResult BuildPlan(TechType targetTechType, int targetAmount)
    {
        return BuildPlan(targetTechType, targetAmount, includeStorage: true);
    }

    public CraftPlanResult BuildPlanInventoryOnly(TechType targetTechType, int targetAmount)
    {
        return BuildPlan(targetTechType, targetAmount, includeStorage: false);
    }

    private CraftPlanResult BuildPlan(TechType targetTechType, int targetAmount, bool includeStorage)
    {
        var state = new PlannerState(stackingCount, nearbyStorage, includeStorage);
        var success = TryCraftTarget(targetTechType, targetAmount, state);
        return new CraftPlanResult(success, state.Consumed, state.Crafted);
    }

    private bool TryCraftTarget(TechType targetTechType, int targetAmount, PlannerState state)
    {
        if (targetTechType == TechType.None || targetAmount <= 0)
        {
            return false;
        }

        var outputPerCraft = GetEffectiveOutputPerCraft(targetTechType);
        var craftsNeeded = (int)Math.Ceiling(targetAmount / (float)outputPerCraft);

        for (var i = 0; i < craftsNeeded; i++)
        {
            if (!TryCraftSingle(targetTechType, state, 0, new HashSet<TechType>()))
            {
                return false;
            }
        }

        return state.GetNetAvailable(targetTechType) >= targetAmount;
    }

    private bool TryCraftSingle(TechType techType, PlannerState state, int depth, HashSet<TechType> path)
    {
        if (depth > MaxPlannerDepth)
        {
            return false;
        }

        if (!path.Add(techType))
        {
            return false;
        }

        var ingredients = TechData.GetIngredients(techType);
        if (ingredients != null)
        {
            foreach (var ingredient in ingredients)
            {
                if (!EnsureAndConsume(ingredient.techType, ingredient.amount, state, depth + 1, path))
                {
                    path.Remove(techType);
                    return false;
                }
            }
        }
        else
        {
            var linkedOnlyItems = TechData.GetLinkedItems(techType);
            if (linkedOnlyItems == null || linkedOnlyItems.Count == 0)
            {
                path.Remove(techType);
                return false;
            }
        }

        state.AddCrafted(techType, GetBaseCraftAmount(techType));

        var linkedItems = TechData.GetLinkedItems(techType);
        if (linkedItems != null)
        {
            foreach (var linkedType in linkedItems)
            {
                state.AddCrafted(linkedType, 1);
            }
        }

        path.Remove(techType);
        return true;
    }

    private bool EnsureAndConsume(TechType ingredientType, int amount, PlannerState state, int depth, HashSet<TechType> path)
    {
        if (amount <= 0)
        {
            return true;
        }

        var available = state.GetNetAvailable(ingredientType);
        if (available < amount)
        {
            var missing = amount - available;
            if (!TryAutocraftMissing(ingredientType, missing, state, depth, path))
            {
                return false;
            }
        }

        if (state.GetNetAvailable(ingredientType) < amount)
        {
            return false;
        }

        state.AddConsumed(ingredientType, amount);
        return true;
    }

    private bool TryAutocraftMissing(TechType ingredientType, int missingAmount, PlannerState state, int depth, HashSet<TechType> path)
    {
        if (!config.EnableAutoSubcraft.Value)
        {
            return false;
        }

        if (!CraftTree.IsCraftable(ingredientType))
        {
            return false;
        }

        var outputPerCraft = GetEffectiveOutputPerCraft(ingredientType);
        var craftsNeeded = (int)Math.Ceiling(missingAmount / (float)outputPerCraft);

        for (var i = 0; i < craftsNeeded; i++)
        {
            if (!TryCraftSingle(ingredientType, state, depth, path))
            {
                return false;
            }
        }

        return true;
    }

    private static int GetBaseCraftAmount(TechType techType)
    {
        var amount = TechData.GetCraftAmount(techType);
        return amount <= 0 ? 1 : amount;
    }

    private static int GetEffectiveOutputPerCraft(TechType techType)
    {
        var output = GetBaseCraftAmount(techType);
        var linkedItems = TechData.GetLinkedItems(techType);
        if (linkedItems == null || linkedItems.Count == 0)
        {
            return output;
        }

        var sameTypeLinked = 0;
        for (var i = 0; i < linkedItems.Count; i++)
        {
            if (linkedItems[i] == techType)
            {
                sameTypeLinked++;
            }
        }

        return output + sameTypeLinked;
    }

    private sealed class PlannerState
    {
        private readonly Dictionary<TechType, int> baseCounts = new Dictionary<TechType, int>();
        private readonly StackingCountService stackingCount;
        private readonly NearbyStorageService nearbyStorage;
        private readonly bool includeStorage;

        public PlannerState(StackingCountService stackingCount, NearbyStorageService nearbyStorage, bool includeStorage)
        {
            this.stackingCount = stackingCount;
            this.nearbyStorage = nearbyStorage;
            this.includeStorage = includeStorage;
        }

        public Dictionary<TechType, int> Consumed { get; } = new Dictionary<TechType, int>();
        public Dictionary<TechType, int> Crafted { get; } = new Dictionary<TechType, int>();

        public int GetNetAvailable(TechType techType)
        {
            var baseCount = GetBaseCount(techType);
            var crafted = Crafted.TryGetValue(techType, out var craftedAmount) ? craftedAmount : 0;
            var consumed = Consumed.TryGetValue(techType, out var consumedAmount) ? consumedAmount : 0;
            return baseCount + crafted - consumed;
        }

        public void AddConsumed(TechType techType, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (!Consumed.ContainsKey(techType))
            {
                Consumed[techType] = 0;
            }

            Consumed[techType] += amount;
        }

        public void AddCrafted(TechType techType, int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (!Crafted.ContainsKey(techType))
            {
                Crafted[techType] = 0;
            }

            Crafted[techType] += amount;
        }

        private int GetBaseCount(TechType techType)
        {
            if (baseCounts.TryGetValue(techType, out var count))
            {
                return count;
            }

            if (Inventory.main == null)
            {
                baseCounts[techType] = 0;
                return 0;
            }

            var inventoryCount = stackingCount.GetContainerCount(Inventory.main.container, techType);
            var storageCount = includeStorage
                ? nearbyStorage.GetNearbyCount(techType, Inventory.main.container)
                : 0;

            count = inventoryCount + storageCount;
            baseCounts[techType] = count;
            return count;
        }
    }
}