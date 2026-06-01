using System.Collections.Generic;
using HarmonyLib;
using SubCraftica.Services.Localization;
using SubCraftica.Services.UI;
using UnityEngine;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyItem))]
internal static class InventoryDestroyItemPatch
{
    private static bool autoCraftConsumeInProgress;
    private static float lastConstructWarningTime;

    [HarmonyPostfix]
    private static void Postfix(Inventory __instance, TechType destroyTechType, ref bool __result)
    {
        RecipeOwnedIngredientsTooltipService.MarkDataDirty();

        if (__result || __instance == null || __instance != Inventory.main || Plugin.Services == null)
        {
            return;
        }

        if (!GameModeUtils.RequiresIngredients())
        {
            return;
        }

        __result = Plugin.Services.NearbyStorage.DestroyInNearby(destroyTechType, 1, __instance.container);
        if (__result)
        {
            return;
        }

        if (Plugin.Services.StackingCount.TryConsumeVirtualUnit(__instance.container, destroyTechType, 1))
        {
            __result = true;
            return;
        }

        if (autoCraftConsumeInProgress || !ConstructableConstructPatchContext.IsActive)
        {
            return;
        }

        if (!Plugin.Services.Config.EnableAutoSubcraft.Value)
        {
            return;
        }

        if (!CraftTree.IsCraftable(destroyTechType))
        {
            return;
        }

        if (TryAutoCraftForConstruct(destroyTechType))
        {
            __result = true;
            return;
        }

        TryShowConstructWarning();
    }

    private static bool TryAutoCraftForConstruct(TechType techType)
    {
        var plan = Plugin.Services.RecipePlanner.BuildPlan(techType, 1);
        if (plan == null || !plan.Success || plan.Consumed == null || plan.Crafted == null)
        {
            return false;
        }

        var netToConsume = BuildNetToConsume(plan.Consumed, plan.Crafted);
        var consumedSoFar = new Dictionary<TechType, int>();

        autoCraftConsumeInProgress = true;
        try
        {
            foreach (var pair in netToConsume)
            {
                for (var i = 0; i < pair.Value; i++)
                {
                    if (!Inventory.main.DestroyItem(pair.Key))
                    {
                        RestoreConsumed(consumedSoFar);
                        return false;
                    }

                    AddCount(consumedSoFar, pair.Key, 1);
                }
            }

            return true;
        }
        finally
        {
            autoCraftConsumeInProgress = false;
        }
    }

    private static Dictionary<TechType, int> BuildNetToConsume(Dictionary<TechType, int> consumed, Dictionary<TechType, int> crafted)
    {
        var result = new Dictionary<TechType, int>();
        foreach (var pair in consumed)
        {
            var produced = crafted.TryGetValue(pair.Key, out var craftedAmount) ? craftedAmount : 0;
            var needed = pair.Value - produced;
            if (needed > 0)
            {
                result[pair.Key] = needed;
            }
        }

        return result;
    }

    private static void RestoreConsumed(Dictionary<TechType, int> consumed)
    {
        foreach (var pair in consumed)
        {
            for (var i = 0; i < pair.Value; i++)
            {
                RestoreSingleItem(pair.Key);
            }
        }
    }

    private static void RestoreSingleItem(TechType techType)
    {
        var gameObject = CraftData.InstantiateFromPrefab(null, techType, false);
        if (gameObject == null)
        {
            return;
        }

        var pickupable = gameObject.GetComponent<Pickupable>();
        if (pickupable == null)
        {
            Object.Destroy(gameObject);
            return;
        }

        pickupable.Pickup(false);
        if (Inventory.main != null && Inventory.main.container != null && Inventory.main.container.AddItem(pickupable) != null)
        {
            return;
        }

        pickupable.Drop(Player.main != null ? Player.main.transform.position + Vector3.down : Vector3.zero, Vector3.down, true);
    }

    private static void TryShowConstructWarning()
    {
        if (Time.unscaledTime < lastConstructWarningTime + 1f)
        {
            return;
        }

        lastConstructWarningTime = Time.unscaledTime;
        ErrorMessage.AddWarning(ModText.Get(ModText.WarningConstructAutoCraftFailed));
    }

    private static void AddCount(Dictionary<TechType, int> dictionary, TechType techType, int value)
    {
        if (dictionary.TryGetValue(techType, out var current))
        {
            dictionary[techType] = current + value;
            return;
        }

        dictionary[techType] = value;
    }
}