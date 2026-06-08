using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(Constructable), "GetConstructInterval")]
internal static class ConstructableGetConstructIntervalPatch
{
    [HarmonyPostfix]
    private static void Postfix(ref float __result)
    {
        if (Plugin.Services?.Config?.CreativeMode?.Value == true)
        {
            __result = 0.01f;
        }
    }
}
