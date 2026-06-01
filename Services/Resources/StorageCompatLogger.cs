using System.Collections.Generic;
using SubCraftica.Services.Stacking;

namespace SubCraftica.Services.Resources;

internal static class StorageCompatLogger
{
    private static readonly HashSet<string> LoggedCompatibilityMessages = new HashSet<string>();

    public static void LogDetectedBackend(StackingBackend backend)
    {
        if (Plugin.Log == null)
        {
            return;
        }

        var backendText = backend == StackingBackend.MadesRedoInventoryStacking
            ? "Mades Redo Inventory Stacking"
            : backend == StackingBackend.InventoryResourceStacks
                ? "Inventory Resource Stacks"
            : "Vanilla";

        Plugin.Log.LogInfo($"Storage and stacking compatibility initialized with backend: {backendText}");
    }

    public static void LogCompatibilityWarningOnce(string key, string message)
    {
        if (Plugin.Log == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (LoggedCompatibilityMessages)
        {
            if (!LoggedCompatibilityMessages.Add($"W:{key}"))
            {
                return;
            }
        }

        Plugin.Log.LogWarning(message);
    }

    public static void LogCompatibilityErrorOnce(string key, string message)
    {
        if (Plugin.Log == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        lock (LoggedCompatibilityMessages)
        {
            if (!LoggedCompatibilityMessages.Add($"E:{key}"))
            {
                return;
            }
        }

        Plugin.Log.LogError(message);
    }
}