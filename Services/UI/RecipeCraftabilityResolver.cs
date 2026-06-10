using System.Collections.Generic;

namespace SubCraftica.Services.UI;

internal static class RecipeCraftabilityResolver
{
    private static ItemsContainer _sessionPlayerContainer;
    private static readonly Dictionary<TechType, int> SessionPlayerCountCache = new Dictionary<TechType, int>();
    private static readonly Dictionary<TechType, int> SessionStorageCountCache = new Dictionary<TechType, int>();
    private static readonly Dictionary<TechType, int> SessionAvailableCountCache = new Dictionary<TechType, int>();
    private static readonly Dictionary<(TechType techType, int requiredAmount), CraftSource> SessionCraftSourceCache = new Dictionary<(TechType techType, int requiredAmount), CraftSource>();

    internal static void BeginEvaluationSession(ItemsContainer playerContainer)
    {
        if (_sessionPlayerContainer == playerContainer)
        {
            return;
        }

        _sessionPlayerContainer = playerContainer;
        SessionPlayerCountCache.Clear();
        SessionStorageCountCache.Clear();
        SessionAvailableCountCache.Clear();
        SessionCraftSourceCache.Clear();
    }

    internal static void EndEvaluationSession()
    {
        _sessionPlayerContainer = null;
        SessionPlayerCountCache.Clear();
        SessionStorageCountCache.Clear();
        SessionAvailableCountCache.Clear();
        SessionCraftSourceCache.Clear();
    }

    internal static bool CanExpandSubrecipe(TechType techType)
    {
        var services = Plugin.Services;
        if (services?.Config == null)
        {
            return false;
        }

        if (!services.Config.EnableAutoSubcraft.Value)
        {
            return false;
        }

        return CraftTree.IsCraftable(techType);
    }

    internal static int GetAvailableCount(TechType techType, ItemsContainer playerContainer)
    {
        if (_sessionPlayerContainer == playerContainer && SessionAvailableCountCache.TryGetValue(techType, out var cached))
        {
            return cached;
        }

        var value = GetPlayerCount(techType, playerContainer) + GetStorageCount(techType, playerContainer);
        if (_sessionPlayerContainer == playerContainer)
        {
            SessionAvailableCountCache[techType] = value;
        }

        return value;
    }

    internal static int GetPlayerCount(TechType techType, ItemsContainer playerContainer)
    {
        if (_sessionPlayerContainer == playerContainer && SessionPlayerCountCache.TryGetValue(techType, out var cached))
        {
            return cached;
        }

        var value = 0;
        if (playerContainer != null && Plugin.Services?.StackingCount != null)
        {
            value = Plugin.Services.StackingCount.GetContainerCount(playerContainer, techType);
        }
        else if (playerContainer != null)
        {
            value = playerContainer.GetCount(techType);
        }

        // Also include equipped items
        if (Plugin.Services?.EquippedResources != null)
        {
            value += Plugin.Services.EquippedResources.GetEquippedCount(techType);
        }

        if (_sessionPlayerContainer == playerContainer)
        {
            SessionPlayerCountCache[techType] = value;
        }

        return value;
    }

    internal static int GetStorageCount(TechType techType, ItemsContainer playerContainer)
    {
        if (_sessionPlayerContainer == playerContainer && SessionStorageCountCache.TryGetValue(techType, out var cached))
        {
            return cached;
        }

        var value = 0;
        if (Plugin.Services?.NearbyStorage != null)
        {
            value = Plugin.Services.NearbyStorage.GetNearbyCount(techType, playerContainer);
        }

        if (_sessionPlayerContainer == playerContainer)
        {
            SessionStorageCountCache[techType] = value;
        }

        return value;
    }

    internal static void ResolveNodeCraftability(IngredientNode node, int missingAmount, int depth, int maxDepth, ItemsContainer playerContainer)
    {
        if (node == null || missingAmount <= 0 || !CanExpandSubrecipe(node.TechType))
        {
            return;
        }

        if (depth <= maxDepth)
        {
            node.Children = BuildSubrecipeNodes(node.TechType, missingAmount, depth, maxDepth, playerContainer);
        }

        var craftSource = GetSubrecipeCraftSource(node.TechType, missingAmount, playerContainer);
        node.CraftableBySubingredients = craftSource != CraftSource.None;
        node.CraftFromStorage = craftSource == CraftSource.Storage;

        if (!node.CraftableBySubingredients)
        {
            var childCraftSource = GetChildCraftSource(node.Children, playerContainer);
            if (childCraftSource != CraftSource.None)
            {
                node.CraftableBySubingredients = true;
                node.CraftFromStorage = childCraftSource == CraftSource.Storage;
            }
        }
    }

    internal static List<IngredientNode> BuildSubrecipeNodes(TechType recipeType, int missingAmount, int depth, int maxDepth, ItemsContainer playerContainer)
    {
        var result = new List<IngredientNode>();
        if (depth > maxDepth)
        {
            return result;
        }

        var ingredients = TechData.GetIngredients(recipeType);
        if (ingredients == null || ingredients.Count == 0)
        {
            return result;
        }

        var yield = TechData.GetCraftAmount(recipeType);
        if (yield <= 0)
        {
            yield = 1;
        }

        var craftsNeeded = UnityEngine.Mathf.CeilToInt(missingAmount / (float)yield);
        var unique = new Dictionary<TechType, int>();
        var order = new List<TechType>();

        for (var i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            var value = UnityEngine.Mathf.Max(0, ingredient.amount * craftsNeeded);
            if (value <= 0)
            {
                continue;
            }

            if (!unique.ContainsKey(ingredient.techType))
            {
                unique[ingredient.techType] = 0;
                order.Add(ingredient.techType);
            }

            unique[ingredient.techType] += value;
        }

        var craftableSubrecipeNodes = new List<IngredientNode>();
        var nonSubrecipeNodes = new List<IngredientNode>();

        for (var i = 0; i < order.Count; i++)
        {
            var type = order[i];
            var required = unique[type];
            var node = new IngredientNode(type, required);

            var available = GetAvailableCount(type, playerContainer);
            var missing = UnityEngine.Mathf.Max(0, required - available);
            if (missing > 0 && CanExpandSubrecipe(type))
            {
                ResolveNodeCraftability(node, missing, depth + 1, maxDepth, playerContainer);
            }

            if (CanExpandSubrecipe(type))
            {
                craftableSubrecipeNodes.Add(node);
            }
            else
            {
                nonSubrecipeNodes.Add(node);
            }
        }

        result.AddRange(craftableSubrecipeNodes);
        result.AddRange(nonSubrecipeNodes);

        return result;
    }

    private static bool AreChildRequirementsCraftable(List<IngredientNode> children, ItemsContainer playerContainer)
    {
        if (children == null || children.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null)
            {
                continue;
            }

            var available = GetAvailableCount(child.TechType, playerContainer);
            if (available >= child.Required)
            {
                continue;
            }

            if (!CanExpandSubrecipe(child.TechType))
            {
                return false;
            }

            if (!child.CraftableBySubingredients && !AreChildRequirementsCraftable(child.Children, playerContainer))
            {
                return false;
            }
        }

        return true;
    }

    private static CraftSource GetSubrecipeCraftSource(TechType techType, int requiredAmount, ItemsContainer playerContainer)
    {
        if (_sessionPlayerContainer == playerContainer)
        {
            var key = (techType, requiredAmount);
            if (SessionCraftSourceCache.TryGetValue(key, out var cached))
            {
                return cached;
            }
        }

        var services = Plugin.Services;
        if (services?.RecipePlanner == null || requiredAmount <= 0)
        {
            return CraftSource.None;
        }

        var fullPlan = services.RecipePlanner.BuildPlan(techType, requiredAmount);
        if (fullPlan == null || !fullPlan.Success)
        {
            return CraftSource.None;
        }

        var inventoryOnlyPlan = services.RecipePlanner.BuildPlanInventoryOnly(techType, requiredAmount);
        if (inventoryOnlyPlan != null && inventoryOnlyPlan.Success)
        {
            return CraftSource.Inventory;
        }

        if (fullPlan.Consumed == null || fullPlan.Consumed.Count == 0)
        {
            return CraftSource.Inventory;
        }

        var usesStorage = false;
        foreach (var pair in fullPlan.Consumed)
        {
            var needed = UnityEngine.Mathf.Max(0, pair.Value);
            if (needed <= 0)
            {
                continue;
            }

            var playerCount = GetPlayerCount(pair.Key, playerContainer);
            if (playerCount >= needed)
            {
                continue;
            }

            var storageCount = GetStorageCount(pair.Key, playerContainer);
            if (playerCount + storageCount >= needed)
            {
                usesStorage = true;
                continue;
            }

            return CraftSource.None;
        }

        var resolved = usesStorage ? CraftSource.Storage : CraftSource.Inventory;
        if (_sessionPlayerContainer == playerContainer)
        {
            SessionCraftSourceCache[(techType, requiredAmount)] = resolved;
        }

        return resolved;
    }

    private static CraftSource GetChildCraftSource(List<IngredientNode> children, ItemsContainer playerContainer)
    {
        if (children == null || children.Count == 0)
        {
            return CraftSource.None;
        }

        var anyStorage = false;

        for (var i = 0; i < children.Count; i++)
        {
            var child = children[i];
            if (child == null)
            {
                continue;
            }

            var playerCount = GetPlayerCount(child.TechType, playerContainer);
            if (playerCount >= child.Required)
            {
                continue;
            }

            var totalAvailable = playerCount + GetStorageCount(child.TechType, playerContainer);
            if (totalAvailable >= child.Required)
            {
                anyStorage = true;
                continue;
            }

            if (!CanExpandSubrecipe(child.TechType))
            {
                return CraftSource.None;
            }

            if (!child.CraftableBySubingredients)
            {
                var nestedSource = GetChildCraftSource(child.Children, playerContainer);
                if (nestedSource == CraftSource.None)
                {
                    return CraftSource.None;
                }

                if (nestedSource == CraftSource.Storage)
                {
                    anyStorage = true;
                }

                continue;
            }

            if (child.CraftFromStorage)
            {
                anyStorage = true;
            }
        }

        return anyStorage ? CraftSource.Storage : CraftSource.Inventory;
    }

    private enum CraftSource
    {
        None,
        Inventory,
        Storage
    }
}

internal sealed class IngredientNode
{
    public IngredientNode(TechType techType, int required)
    {
        TechType = techType;
        Required = required < 0 ? 0 : required;
        Children = new List<IngredientNode>();
    }

    public TechType TechType { get; }
    public int Required { get; }
    public List<IngredientNode> Children { get; set; }
    public bool CraftableBySubingredients { get; set; }
    public bool CraftFromStorage { get; set; }
}
