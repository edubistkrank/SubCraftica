using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SubCraftica.Patches;

/// <summary>
/// Patches uGUI_RecipeEntry.UpdateIngredients so that the pinned-recipe tracker
/// on the right side of the screen counts ingredients from inventory + nearby
/// storage (respecting the player's configured storage mode), instead of only
/// from the player's direct inventory container.
/// </summary>
[HarmonyPatch(typeof(uGUI_RecipeEntry), nameof(uGUI_RecipeEntry.UpdateIngredients), new[] { typeof(ItemsContainer), typeof(bool) })]
internal static class uGUIPinnedRecipesUpdateIngredientsPatch
{
    private static readonly Dictionary<string, int> LastTotalCounts = new Dictionary<string, int>();

    private static readonly FieldInfo ItemsField =
        AccessTools.Field(typeof(uGUI_RecipeEntry), "items");

    private static readonly FieldInfo MinField =
        AccessTools.Field(typeof(uGUI_RecipeEntry), "min");

    [HarmonyPostfix]
    private static void Postfix(uGUI_RecipeEntry __instance, ItemsContainer container, bool ping)
    {
        if (__instance == null || container == null || Plugin.Services == null)
        {
            return;
        }

        if (!(ItemsField?.GetValue(__instance) is List<uGUI_RecipeItem> items) || items.Count == 0)
        {
            return;
        }

        var ingredients = TechData.GetIngredients(__instance.techType);
        if (ingredients == null || ingredients.Count == 0)
        {
            return;
        }

        var manager = __instance.manager;
        if (manager == null)
        {
            return;
        }

        var itemCount = Math.Min(items.Count, ingredients.Count);
        var craftsAvailable = -1;

        for (var i = 0; i < itemCount; i++)
        {
            var ingredient = ingredients[i];
            var amount = ingredient.amount;
            if (amount <= 0)
            {
                amount = 1;
            }

            var count = GetTotalCount(container, ingredient.techType);
            var craftsForIngredient = count / amount;
            if (craftsAvailable < 0 || craftsForIngredient < craftsAvailable)
            {
                craftsAvailable = craftsForIngredient;
            }

            var recipeItem = items[i];
            if (recipeItem == null)
            {
                continue;
            }

            var cacheKey = BuildIngredientKey(__instance, ingredient.techType);
            LastTotalCounts.TryGetValue(cacheKey, out var previousCount);
            var shouldPing = ping && count > previousCount;
            LastTotalCounts[cacheKey] = count;

            var text = recipeItem.text;
            if (text != null)
            {
                text.color = count >= amount ? manager.colorGreen : manager.colorRed;
            }

            recipeItem.Set(ingredient.techType, count, amount, shouldPing);
        }

        var craftAmount = TechData.GetCraftAmount(__instance.techType);
        if (craftAmount <= 0)
        {
            craftAmount = 1;
        }

        var totalCrafts = craftsAvailable * craftAmount;
        if (totalCrafts > 0)
        {
            MinField?.SetValue(__instance, totalCrafts);
            if (__instance.text != null)
            {
                __instance.text.text = $"x{IntStringCache.GetStringForInt(totalCrafts)}";
            }
        }
        else
        {
            MinField?.SetValue(__instance, int.MinValue);
            if (__instance.text != null)
            {
                __instance.text.text = string.Empty;
            }
        }
    }

    /// <summary>
    /// Returns the total count of <paramref name="techType"/> visible from the
    /// player's inventory plus all eligible nearby/base/pod storage containers.
    /// </summary>
    private static int GetTotalCount(ItemsContainer container, TechType techType)
    {
        if (container == null)
        {
            return 0;
        }

        if (Plugin.Services == null)
        {
            return container.GetCount(techType);
        }

        var inventory = Plugin.Services.StackingCount.GetContainerCount(container, techType);
        var storage = Plugin.Services.NearbyStorage.GetNearbyCount(techType, container);
        return inventory + storage;
    }

    internal static void ClearPingCache()
    {
        LastTotalCounts.Clear();
    }

    private static string BuildIngredientKey(uGUI_RecipeEntry entry, TechType techType)
    {
        return entry.GetInstanceID().ToString() + "|" + ((int)techType).ToString();
    }
}
