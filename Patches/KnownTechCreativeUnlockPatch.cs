using System.Reflection;
using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(KnownTech), nameof(KnownTech.GetTechUnlockState), new[] { typeof(TechType) })]
internal static class KnownTechGetTechUnlockStateCreativePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref TechUnlockState __result)
    {
        if (Plugin.Services == null || !Plugin.Services.Config.CreativeMode.Value)
            return;
        __result = TechUnlockState.Available;
    }
}

[HarmonyPatch]
internal static class KnownTechGetTechUnlockStateWithIndexesCreativePatch
{
    static MethodBase TargetMethod() =>
        typeof(KnownTech).GetMethod(nameof(KnownTech.GetTechUnlockState), new[] { typeof(TechType), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });

    [HarmonyPostfix]
    private static void Postfix(ref TechUnlockState __result)
    {
        if (Plugin.Services == null || !Plugin.Services.Config.CreativeMode.Value)
            return;
        __result = TechUnlockState.Available;
    }
}

[HarmonyPatch(typeof(KnownTech), nameof(KnownTech.Contains), new[] { typeof(TechType) })]
internal static class KnownTechContainsCreativePatch
{
    [HarmonyPostfix]
    private static void Postfix(ref bool __result)
    {
        if (Plugin.Services == null || !Plugin.Services.Config.CreativeMode.Value)
            return;
        __result = true;
    }
}
