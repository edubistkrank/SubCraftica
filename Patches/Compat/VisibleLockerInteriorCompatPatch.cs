using System;
using System.Reflection;
using SubCraftica.Services.Resources;
using UnityEngine;

namespace SubCraftica.Patches.Compat;

internal static class VisibleLockerInteriorCompatPatch
{
    internal const string PluginGuid = "com.russjudge.visiblelockerinterior";

    private const string UpdateInteriorWarningKey = "VisibleLockerInterior.UpdateInterior";
    private static MethodInfo updateInteriorMethod;
    private static float nextResolveAttemptAt;
    private static bool missingLogged;

    internal static void TryRefresh(Component owner)
    {
        if (!(owner is StorageContainer storageContainer))
        {
            return;
        }

        if (updateInteriorMethod == null && Time.unscaledTime >= nextResolveAttemptAt)
        {
            nextResolveAttemptAt = Time.unscaledTime + 2f;
            var controllerType = CompatReflectionHelper.FindType("VisibleLockerInterior.Controller", "VisibleLockerInterior");

            updateInteriorMethod = controllerType?.GetMethod(
                "UpdateInterior",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(StorageContainer) },
                null);
        }

        if (updateInteriorMethod == null)
        {
            if (!missingLogged && Time.unscaledTime > 5f)
            {
                missingLogged = true;
                Plugin.Log?.LogInfo("VisibleLockerInterior compatibility: update method not resolved yet.");
            }

            return;
        }

        try
        {
            updateInteriorMethod.Invoke(null, new object[] { storageContainer });
        }
        catch (TargetInvocationException ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(UpdateInteriorWarningKey, $"VisibleLockerInterior refresh failed: {ex.InnerException?.Message ?? ex.Message}");
        }
        catch (Exception ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(UpdateInteriorWarningKey, $"VisibleLockerInterior refresh failed: {ex.Message}");
        }
    }
}
