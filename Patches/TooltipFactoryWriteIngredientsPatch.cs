using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using System.Reflection;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.UI;

namespace SubCraftica.Patches
{
    [HarmonyPatch]
    internal static class TooltipFactoryWriteIngredientsPatch
    {
        private static MethodBase TargetMethod()
        {
            var t = typeof(TooltipFactory);
            foreach (var m in t.GetMethods(BindingFlags.NonPublic | BindingFlags.Static))
            {
                if (m.Name != "WriteIngredients")
                    continue;

                var ps = m.GetParameters();
                if (ps.Length == 2 && ps[0].ParameterType.IsGenericType)
                    return m;
            }

            return null;
        }

        [HarmonyPrefix]
        private static bool Prefix(IList<Ingredient> ingredients, List<TooltipIcon> icons)
        {
            if (ingredients == null)
                return false;

            if (Plugin.Services == null)
                return true;

            RecipeOwnedIngredientsTooltipService.MarkDataDirty();

            if (icons == null)
                return false;

            icons.Clear();

            var count = ingredients.Count;
            var main = Inventory.main;
            var sb = new StringBuilder();

            for (var i = 0; i < count; i++)
            {
                sb.Length = 0;

                var ingredient = ingredients[i];
                var techType = ingredient.techType;
                var amount = ingredient.amount;

                var pickupCount = 0;
                var playerContainer = main?.container;
                if (playerContainer != null && Plugin.Services.StackingCount != null)
                {
                    pickupCount = Plugin.Services.StackingCount.GetContainerCount(playerContainer, techType);
                }
                else if (playerContainer != null)
                {
                    pickupCount = playerContainer.GetCount(techType);
                }
                else if (main != null)
                {
                    pickupCount = main.GetPickupCount(techType);
                }

                var inventoryEnough = pickupCount >= amount || !GameModeUtils.RequiresIngredients();
                var sprite = SpriteManager.Get(techType);

                if (inventoryEnough)
                {
                    var invColors = StorageIngredientColorPresets.Colors;
                    var invIdx = Plugin.Services.Config.InventoryIngredientColorPreset.Value;
                    if (invIdx < 0 || invIdx >= invColors.Length)
                        invIdx = 0;
                    sb.Append("<color=#");
                    sb.Append(ColorHexUtility.ToHex(invColors[invIdx]));
                    sb.Append(">");
                }
                else
                {
                    var storageCount = Plugin.Services.NearbyStorage.GetNearbyCount(techType, playerContainer);
                    if (pickupCount + storageCount >= amount)
                    {
                        var storColors = StorageIngredientColorPresets.Colors;
                        var storIdx = Plugin.Services.Config.StorageOnlyIngredientColorPreset.Value;
                        if (storIdx < 0 || storIdx >= storColors.Length)
                            storIdx = 0;
                        sb.Append("<color=#");
                        sb.Append(ColorHexUtility.ToHex(storColors[storIdx]));
                        sb.Append(">");
                    }
                    else
                    {
                        var misColors = MissingIngredientColorPresets.Colors;
                        var misIdx = Plugin.Services.Config.MissingIngredientColorPreset.Value;
                        if (misIdx < 0 || misIdx >= misColors.Length)
                            misIdx = 0;
                        sb.Append("<color=#");
                        sb.Append(ColorHexUtility.ToHex(misColors[misIdx]));
                        sb.Append(">");
                    }
                }

                var name = techType.AsString(false);
                sb.Append(name);

                if (amount > 1)
                {
                    sb.Append(" x");
                    sb.Append(amount);
                }

                sb.Append("</color>");
                icons.Add(new TooltipIcon(sprite, sb.ToString()));
            }

            return false;
        }

    }
}
