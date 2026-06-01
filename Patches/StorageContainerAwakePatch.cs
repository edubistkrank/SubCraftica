using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(StorageContainer))]
internal static class StorageContainerAwakePatch
{
    [HarmonyPatch(nameof(StorageContainer.Awake))]
    [HarmonyPostfix]
    private static void AwakePostfix(StorageContainer __instance)
    {
        if (__instance?.container == null || Plugin.Services == null)
        {
            return;
        }

        Plugin.Services.NearbyStorage.Register(__instance, __instance.container);
    }
}