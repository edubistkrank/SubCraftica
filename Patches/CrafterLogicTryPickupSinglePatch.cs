using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Stacking;
using SubCraftica.Services.UI;
using UnityEngine;

namespace SubCraftica.Patches;

[HarmonyPatch]
internal static class CrafterLogicTryPickupSinglePatch
{
    private static MethodBase TargetMethod()
    {
        return typeof(CrafterLogic).GetMethod(
            "TryPickupSingleAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
    }

    [HarmonyPrefix]
    private static bool Prefix(CrafterLogic __instance, TechType techType, IOut<bool> result, ref IEnumerator __result)
    {
        if (Plugin.Services == null)
        {
            return true;
        }

        var useStorageFallback = Plugin.Services.Config.ReturnSurplusToStorage.Value
            && Plugin.Services.Config.StorageCraftMode.Value != Services.Configuration.ModConfig.StorageModeDisabled;
        var useStackingPickupCompat = Plugin.Services.StackingDetection.Backend != StackingBackend.Vanilla;

        // Always run the compatibility pickup pipeline so each produced unit is handled
        // immediately, even when the crafting menu is closed or the player is far away.
        __result = TryPickupWithCompatibility(__instance, techType, result, useStorageFallback, useStackingPickupCompat);
        return false;
    }

    // --- Orchestrator ---

    private static IEnumerator TryPickupWithCompatibility(CrafterLogic instance, TechType techType, IOut<bool> result, bool useStorageFallback, bool useStackingPickupCompat)
    {
        var coordinator = Plugin.Services?.QueueCoordinator;
        coordinator?.BeginPickupOperation();
        try
        {
            // Phase 1: resolve prefab
            var resolveResult = ResolvePrefabAsync(techType);
            yield return resolveResult.Request;

            if (!resolveResult.TryGetPrefab(techType, out var prefab, out var overrideTech))
            {
                result.Set(true);
                yield break;
            }

            // Phase 2: try inventory
            var go = UnityEngine.Object.Instantiate(prefab);
            var pickupable = go.GetComponent<Pickupable>();
            if (overrideTech)
            {
                pickupable.SetTechTypeOverride(techType, true);
            }

            if (TryPlaceInInventory(instance, go, pickupable, useStackingPickupCompat))
            {
                result.Set(true);
                RecipeOwnedIngredientsTooltipService.MarkDataDirty();
                yield break;
            }

            // Phase 3: try storage fallback
            if (useStorageFallback && TryPlaceInStorage(instance, go, pickupable, coordinator))
            {
                result.Set(true);
                RecipeOwnedIngredientsTooltipService.MarkDataDirty();
                yield break;
            }

            // Phase 4: hard failure (inventory + optional storage full)
            HandlePickupFailure(go, useStorageFallback, coordinator);
            result.Set(false);
            RecipeOwnedIngredientsTooltipService.MarkDataDirty();
        }
        finally
        {
            coordinator?.EndPickupOperation();
        }
    }

    // --- Phase helpers ---

    private static PrefabResolveResult ResolvePrefabAsync(TechType techType)
    {
        var request = CraftData.GetPrefabForTechTypeAsync(techType, true);
        return new PrefabResolveResult(request);
    }

    private static bool TryPlaceInInventory(CrafterLogic instance, GameObject go, Pickupable pickupable, bool useStackingPickupCompat)
    {
        var inventory = Inventory.main;
        var itemSize = TechData.GetItemSize(pickupable.GetTechType());

        if (inventory.HasRoomFor(itemSize.x, itemSize.y))
        {
            CrafterLogic.NotifyCraftEnd(go, instance.craftingTechType);
            inventory.ForcePickup(pickupable);
            Player.main.PlayGrab();
            NotifyPickup(instance, go);
            return true;
        }

        if (!useStackingPickupCompat)
        {
            return false;
        }

        // Stacking compat path: Pickup + AddItem
        pickupable.Pickup(false);
        if (inventory.container != null && inventory.container.AddItem(pickupable) != null)
        {
            CrafterLogic.NotifyCraftEnd(go, instance.craftingTechType);
            Player.main.PlayGrab();
            NotifyPickup(instance, go);
            return true;
        }

        return false;
    }

    private static bool TryPlaceInStorage(CrafterLogic instance, GameObject go, Pickupable pickupable, Services.Crafting.CraftQueueCoordinatorService coordinator)
    {
        // Match container-insertion semantics used by stacking compat inventory path.
        // This prevents world-entity side effects on items routed to storage.
        pickupable.Pickup(false);

        if (!Plugin.Services.NearbyStorage.TryAddToNearbyStorage(pickupable))
        {
            return false;
        }
        CrafterLogic.NotifyCraftEnd(go, instance.craftingTechType);
        NotifyPickup(instance, go);

        // IMPORTANT: Do not destroy the source GameObject after AddItem success.
        // The stored Pickupable may still be the same object instance and destroying it
        // can remove the item that was just inserted into storage.
        if (coordinator != null && !coordinator.StorageMoveNoticeShown)
        {
            ErrorMessage.AddWarning(ModText.Get(ModText.WarningInventoryMovedToStorage));
            coordinator.MarkStorageMoveNoticeShown();
        }

        return true;
    }

    private static void HandlePickupFailure(GameObject go, bool useStorageFallback, Services.Crafting.CraftQueueCoordinatorService coordinator)
    {
        UnityEngine.Object.Destroy(go);

        // Vanilla message recolored by ErrorMessageInventoryFullPatch; always shown.
        ErrorMessage.AddMessage(Language.main.Get("InventoryFull"));

        if (useStorageFallback)
        {
            // Additional mod warning: storage was also tried and failed. Shown only when storage fallback is active.
            ErrorMessage.AddWarning(ModText.Get(ModText.WarningNoSpaceInventoryAndStorage));
        }

        // Always stop continuation after pickup failure to avoid overwriting/losing crafted outputs
        // across subsequent queued crafts when inventory/storage cannot receive items.
        coordinator?.RequestStopQueueContinuation(Plugin.Services?.Queue);
        coordinator?.ResetForQueueEnd();
    }

    // --- Utilities ---

    private static void NotifyPickup(CrafterLogic instance, GameObject go)
    {
        var onItemPickup = typeof(CrafterLogic)
            .GetField("onItemPickup", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(instance) as CrafterLogic.OnItemPickup;

        onItemPickup?.Invoke(go);
    }

    // Wraps the async prefab request to keep the coroutine orchestrator readable
    private readonly struct PrefabResolveResult
    {
        public readonly IEnumerator Request;
        private readonly CoroutineTask<GameObject> _request;

        public PrefabResolveResult(CoroutineTask<GameObject> request)
        {
            _request = request;
            Request = (IEnumerator)request;
        }

        public bool TryGetPrefab(TechType techType, out GameObject prefab, out bool overrideTech)
        {
            prefab = _request.GetResult();
            overrideTech = false;

            if (prefab == null)
            {
                prefab = Utils.genericLootPrefab;
                overrideTech = true;
            }

            if (prefab == null)
            {
                return false;
            }

            var pickupable = prefab.GetComponent<Pickupable>();
            if (pickupable == null)
            {
                Plugin.Log.LogError($"[ReturnSurplus] No Pickupable on prefab for {techType}");
                return false;
            }

            return true;
        }
    }
}

[HarmonyPatch(typeof(ErrorMessage), nameof(ErrorMessage.AddMessage), new[] { typeof(string) })]
internal static class ErrorMessageInventoryFullPatch
{
    [ThreadStatic]
    private static bool recolorInProgress;

    [HarmonyPrefix]
    private static bool Prefix(string message)
    {
        if (recolorInProgress)
        {
            return true;
        }

        var inventoryFullMessage = Language.main != null ? Language.main.Get("InventoryFull") : null;
        if (string.IsNullOrWhiteSpace(inventoryFullMessage) || !string.Equals(message, inventoryFullMessage, StringComparison.Ordinal))
        {
            return true;
        }

        recolorInProgress = true;
        try
        {
            ErrorMessage.AddMessage($"<color=#DF4026FF>{message}</color>");
        }
        finally
        {
            recolorInProgress = false;
        }

        return false;
    }
}
