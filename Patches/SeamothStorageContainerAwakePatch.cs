using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(SeamothStorageContainer))]
internal static class SeamothStorageContainerAwakePatch
{
    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    private static void AwakePostfix(SeamothStorageContainer __instance)
    {
        if (__instance?.container == null || Plugin.Services == null)
        {
            return;
        }

        Plugin.Services.NearbyStorage.Register(__instance, __instance.container);
    }
}

[HarmonyPatch(typeof(SeaMoth), "OnUpgradeModuleChange")]
internal static class SeaMothUpgradeModuleChangePatch
{
    [HarmonyPostfix]
    private static void Postfix(SeaMoth __instance)
    {
        if (__instance == null || Plugin.Services == null)
        {
            return;
        }

        foreach (var storage in __instance.GetComponentsInChildren<SeamothStorageContainer>(true))
        {
            if (storage?.container != null)
            {
                Plugin.Services.NearbyStorage.Register(storage, storage.container);
            }
        }
    }
}