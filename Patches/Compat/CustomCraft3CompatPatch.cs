using BepInEx.Bootstrap;
using Nautilus.Crafting;

namespace SubCraftica.Patches.Compat;

internal static class CustomCraft3CompatPatch
{
    internal const string PluginGuid = "com.mrpurple6411.CustomCraft3";

    internal static bool IsInstalled => Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(PluginGuid);

    internal static bool IsLinkedOutputRecipe(RecipeData recipeData)
    {
        return recipeData?.LinkedItems != null
               && recipeData.LinkedItems.Count > 0
               && recipeData.craftAmount <= 1;
    }

    internal static bool UsesPerItemBatching(RecipeData recipeData, int amount)
    {
        return IsInstalled && amount > 1 && IsLinkedOutputRecipe(recipeData);
    }
}
