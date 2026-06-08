using System.Collections.Generic;
using HarmonyLib;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Localization;
using SubCraftica.Services.UI;
using UnityEngine;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(Inventory), nameof(Inventory.DestroyItem))]
internal static class InventoryDestroyItemPatch
{
    private static bool autoCraftConsumeInProgress;
    private static float lastConstructWarningTime;
    private static readonly Dictionary<TechType, int> constructBufferedOutputs = new Dictionary<TechType, int>();

    [HarmonyPostfix]
    private static void Postfix(Inventory __instance, TechType destroyTechType, ref bool __result)
    {
        RecipeOwnedIngredientsTooltipService.MarkDataDirty();

        if (__result || __instance == null || __instance != Inventory.main || Plugin.Services == null)
        {
            return;
        }

        if (CreativeModeHelper.IsCreativeBypassActive(destroyTechType) && IsCraftOrConstructContextActive())
        {
            __result = true;
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

        if (TryConsumeBufferedForAutoCraft(destroyTechType))
        {
            __result = true;
            return;
        }

        if (autoCraftConsumeInProgress || !IsCraftOrConstructContextActive())
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

        if (TryAutoCraftForMissingIngredient(destroyTechType))
        {
            __result = true;
            return;
        }

        TryShowConstructWarning();
    }

    private static bool IsCraftOrConstructContextActive()
    {
        if (ConstructableConstructPatchContext.IsActive)
        {
            return true;
        }

        return Plugin.Services != null && Plugin.Services.Synchronization.IsCraftInProgress;
    }

    internal static void FlushConstructSurplus()
    {
        if (constructBufferedOutputs.Count == 0)
        {
            return;
        }

        foreach (var pair in constructBufferedOutputs)
        {
            var remaining = pair.Value;
            if (remaining <= 0)
            {
                continue;
            }

            while (remaining > 0)
            {
                GiveProducedItem(pair.Key);
                remaining--;
            }
        }

        constructBufferedOutputs.Clear();
        RecipeOwnedIngredientsTooltipService.MarkDataDirty();
    }

    private static bool TryAutoCraftForMissingIngredient(TechType techType)
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

            if (ConstructableConstructPatchContext.IsActive)
            {
                BufferNetCrafted(plan.Consumed, plan.Crafted);
                return TryConsumeBufferedForAutoCraft(techType);
            }

            return ConsumeAutoCraftResultImmediately(plan.Consumed, plan.Crafted, techType);
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

    private static void BufferNetCrafted(Dictionary<TechType, int> consumed, Dictionary<TechType, int> crafted)
    {
        foreach (var pair in crafted)
        {
            var used = consumed.TryGetValue(pair.Key, out var consumedAmount) ? consumedAmount : 0;
            var netProduced = pair.Value - used;
            if (netProduced <= 0)
            {
                continue;
            }

            AddCount(constructBufferedOutputs, pair.Key, netProduced);
        }
    }

    private static bool TryConsumeBufferedForAutoCraft(TechType techType)
    {
        if (!ConstructableConstructPatchContext.IsActive)
        {
            return false;
        }

        if (!constructBufferedOutputs.TryGetValue(techType, out var buffered) || buffered <= 0)
        {
            return false;
        }

        buffered--;
        if (buffered <= 0)
        {
            constructBufferedOutputs.Remove(techType);
        }
        else
        {
            constructBufferedOutputs[techType] = buffered;
        }

        return true;
    }

    private static bool ConsumeAutoCraftResultImmediately(Dictionary<TechType, int> consumed, Dictionary<TechType, int> crafted, TechType requestedType)
    {
        var usedForRequested = consumed.TryGetValue(requestedType, out var consumedAmount) ? consumedAmount : 0;
        var producedForRequested = crafted.TryGetValue(requestedType, out var craftedAmount) ? craftedAmount : 0;
        var netForRequested = producedForRequested - usedForRequested;
        if (netForRequested <= 0)
        {
            return false;
        }

        foreach (var pair in crafted)
        {
            var used = consumed.TryGetValue(pair.Key, out var usedAmount) ? usedAmount : 0;
            var netProduced = pair.Value - used;

            // One unit of requestedType is consumed by the current DestroyItem call.
            if (pair.Key == requestedType)
            {
                netProduced--;
            }

            if (netProduced <= 0)
            {
                continue;
            }

            for (var i = 0; i < netProduced; i++)
            {
                GiveProducedItem(pair.Key);
            }
        }

        return true;
    }

    private static void RestoreConsumed(Dictionary<TechType, int> consumed)
    {
        foreach (var pair in consumed)
        {
            for (var i = 0; i < pair.Value; i++)
            {
                GiveProducedItem(pair.Key);
            }
        }
    }

    private static void GiveProducedItem(TechType techType)
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

        if (Plugin.Services?.NearbyStorage != null && Plugin.Services.NearbyStorage.TryAddToNearbyStorage(pickupable))
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