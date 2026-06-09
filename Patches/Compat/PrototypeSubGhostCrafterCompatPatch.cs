using System;
using System.Reflection;
using HarmonyLib;
using SubCraftica.Services.Logging;

namespace SubCraftica.Patches.Compat;

[HarmonyPatch]
internal static class PrototypeSubGhostCrafterCraftCompatPatch
{
    private static readonly MethodInfo GhostCrafterPrefixMethod =
        AccessTools.Method(typeof(GhostCrafterCraftPatch), "Prefix", new[] { typeof(GhostCrafter), typeof(TechType) });

    private static readonly MethodInfo GhostCrafterFinalizerMethod =
        AccessTools.Method(typeof(GhostCrafterCraftPatch), "Finalizer", new[] { typeof(Exception), typeof(TechType) });

    private static MethodBase TargetMethod()
    {
        var alienFabricatorType = AccessTools.TypeByName("PrototypeSubMod.Prefabs.AlienFabricator");
        if (alienFabricatorType == null)
        {
            SubCrafticaLogger.LogWarning("[PrototypeSubCompat] AlienFabricator type not found - Prototype Sub may not be installed");
            return null;
        }

        var craftMethod = AccessTools.Method(alienFabricatorType, "Craft", new[] { typeof(TechType), typeof(float) });
        if (craftMethod == null)
        {
            SubCrafticaLogger.LogError("[PrototypeSubCompat] Craft(TechType, float) method not found on AlienFabricator");
            return null;
        }

        SubCrafticaLogger.LogInfo("[PrototypeSubCompat] Successfully resolved AlienFabricator.Craft method for patching");
        return craftMethod;
    }

    [HarmonyPrefix]
    private static bool Prefix(GhostCrafter __instance, TechType techType)
    {
        var instanceType = __instance?.GetType().FullName ?? "null";
        SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Prefix] Called for instance type={instanceType}, techType={techType}");

        if (GhostCrafterPrefixMethod == null)
        {
            SubCrafticaLogger.LogError("[PrototypeSubCompat.Prefix] GhostCrafterPrefixMethod is null - forwarding logic unavailable");
            return true;
        }

        try
        {
            SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Prefix] Invoking GhostCrafterCraftPatch.Prefix for techType={techType}");
            var result = GhostCrafterPrefixMethod.Invoke(null, new object[] { __instance, techType });

            if (result is bool allowOriginal)
            {
                SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Prefix] GhostCrafterCraftPatch.Prefix returned {allowOriginal}");
                if (!allowOriginal)
                {
                    SubCrafticaLogger.LogDebug("[PrototypeSubCompat.Prefix] Queue/Batch logic blocked craft, suppressing AlienFabricator.Craft");
                }
                return allowOriginal;
            }

            SubCrafticaLogger.LogWarning($"[PrototypeSubCompat.Prefix] GhostCrafterCraftPatch.Prefix returned unexpected type: {result?.GetType().FullName ?? "null"}");
        }
        catch (TargetInvocationException tiex)
        {
            SubCrafticaLogger.LogError($"[PrototypeSubCompat.Prefix] TargetInvocationException: {tiex.InnerException?.Message}");
            if (tiex.InnerException is Exception innerEx)
            {
                SubCrafticaLogger.LogError($"[PrototypeSubCompat.Prefix] Inner exception details: {innerEx}");
            }
        }
        catch (Exception ex)
        {
            SubCrafticaLogger.LogError($"[PrototypeSubCompat.Prefix] Could not forward AlienFabricator craft prefix: {ex.Message}\n{ex.StackTrace}");
        }

        SubCrafticaLogger.LogDebug("[PrototypeSubCompat.Prefix] Returning true to allow original execution");
        return true;
    }

    [HarmonyFinalizer]
    private static Exception Finalizer(Exception __exception, TechType techType)
    {
        if (__exception != null)
        {
            SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Finalizer] Called with exception: {__exception.GetType().Name} - {__exception.Message}");
        }
        else
        {
            SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Finalizer] Called successfully for techType={techType}");
        }

        if (GhostCrafterFinalizerMethod == null)
        {
            SubCrafticaLogger.LogError("[PrototypeSubCompat.Finalizer] GhostCrafterFinalizerMethod is null");
            return __exception;
        }

        try
        {
            SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Finalizer] Invoking GhostCrafterCraftPatch.Finalizer");
            var result = GhostCrafterFinalizerMethod.Invoke(null, new object[] { __exception, techType });

            if (result == null)
            {
                return null;
            }

            if (result is Exception forwarded)
            {
                if (forwarded != __exception)
                {
                    SubCrafticaLogger.LogDebug($"[PrototypeSubCompat.Finalizer] GhostCrafterCraftPatch.Finalizer transformed exception");
                }
                return forwarded;
            }

            SubCrafticaLogger.LogWarning($"[PrototypeSubCompat.Finalizer] GhostCrafterCraftPatch.Finalizer returned unexpected type: {result.GetType().FullName}");
        }
        catch (TargetInvocationException tiex)
        {
            SubCrafticaLogger.LogError($"[PrototypeSubCompat.Finalizer] TargetInvocationException: {tiex.InnerException?.Message}");
        }
        catch (Exception ex)
        {
            SubCrafticaLogger.LogError($"[PrototypeSubCompat.Finalizer] Could not forward AlienFabricator craft finalizer: {ex.Message}\n{ex.StackTrace}");
        }

        return __exception;
    }
}
