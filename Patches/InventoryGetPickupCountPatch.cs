using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(Inventory), nameof(Inventory.GetPickupCount))]
internal static class InventoryGetPickupCountPatch
{
    [HarmonyPostfix]
    private static void Postfix(Inventory __instance, TechType pickupType, ref int __result)
    {
        if (__instance == null || __instance != Inventory.main || Plugin.Services == null)
        {
            return;
        }

        var inventoryUnits = Plugin.Services.StackingCount.GetContainerCount(__instance.container, pickupType);
        var nearbyUnits = Plugin.Services.NearbyStorage.GetNearbyCount(pickupType, __instance.container);

        __result = inventoryUnits + nearbyUnits;
    }
}