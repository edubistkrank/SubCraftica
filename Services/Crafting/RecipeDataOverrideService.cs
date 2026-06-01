using System.Collections.Generic;
using Nautilus.Crafting;
using Nautilus.Handlers;

namespace SubCraftica.Services.Crafting;

internal sealed class RecipeDataOverrideService
{
    private readonly Dictionary<TechType, RecipeData> originals = new Dictionary<TechType, RecipeData>();

    public bool IsOverridden(TechType techType)
    {
        return techType != TechType.None && originals.ContainsKey(techType);
    }

    public void ApplyAmountOverride(TechType techType, int amount)
    {
        if (techType == TechType.None || amount <= 1)
        {
            return;
        }

        var original = CraftDataHandler.GetRecipeData(techType);
        if (original == null)
        {
            return;
        }

        if (!originals.ContainsKey(techType))
        {
            originals[techType] = original;
        }

        var overridden = new RecipeData
        {
            craftAmount = original.craftAmount * amount,
            Ingredients = new List<Ingredient>(),
            LinkedItems = new List<TechType>(original.LinkedItems)
        };

        foreach (var ingredient in original.Ingredients)
        {
            overridden.Ingredients.Add(new Ingredient(ingredient.techType, ingredient.amount * amount));
        }

        CraftDataHandler.SetRecipeData(techType, overridden);
    }

    public void Restore(TechType techType)
    {
        if (!originals.TryGetValue(techType, out var original))
        {
            return;
        }

        CraftDataHandler.SetRecipeData(techType, original);
        originals.Remove(techType);
    }
}
