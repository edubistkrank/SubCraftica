using System;
using System.Collections;
using System.Reflection;
using SubCraftica.Services.Resources;
using UnityEngine;
using System.Collections.Generic;

namespace SubCraftica.Patches.Compat;

internal static class InventoryResourceStacksCompatPatch
{
    private const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string InitializeWarningKey = "InventoryResourceStacks.Initialize";
    private const string GetContainerCountWarningKey = "InventoryResourceStacks.GetContainerCount";
    private const string TryConsumeWarningKey = "InventoryResourceStacks.TryConsumeVirtualUnit";
    private const string ResyncWarningKey = "InventoryResourceStacks.TryResyncMaterializedItems";
    private const string RefillingWarningKey = "InventoryResourceStacks.TryResyncMaterializedItems.Refilling";

    internal const string PluginGuid = "com.Complot69.virtualstack";
    internal const string AssemblyName = "PruebaDificultad";
    internal const string PluginTypeName = "InventoryStacks.VirtualStackPlugin";
    internal const string SaveDataTypeName = "InventoryStacks.ModSaveData";

    private static Type pluginType;
    private static MethodInfo materializeMethod;
    private static PropertyInfo mainSaveDataProperty;
    private static FieldInfo extrasField;
    private static FieldInfo materialsField;
    private static FieldInfo isRefillingField;
    private static int lastResyncFrame = -1;
    // Prevent rapid repeated materialization of the same techtype which can cause
    // pickup/pop-up spam when players hold inputs or deconstruct repeatedly.
    private static readonly Dictionary<TechType, float> lastMaterializeAt = new Dictionary<TechType, float>();
    private const float MaterializeCooldownSeconds = 0.5f;

    internal static void Initialize()
    {
        pluginType = CompatReflectionHelper.FindType(PluginTypeName, AssemblyName);
        if (pluginType == null)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(InitializeWarningKey, $"Could not find Inventory Resource Stacks plugin type '{PluginTypeName}'."); ;
            return;
        }

        materializeMethod = pluginType.GetMethod("Materialize", AllFlags, null, new[] { typeof(TechType), typeof(int) }, null);
        mainSaveDataProperty = pluginType.GetProperty("MainSaveData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        materialsField = pluginType.GetField("MaterialesApilables", AllFlags);
        isRefillingField = pluginType.GetField("isRefilling", AllFlags);

        var saveDataType = CompatReflectionHelper.FindType(SaveDataTypeName, AssemblyName);
        extrasField = saveDataType?.GetField("extras", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (materializeMethod == null || mainSaveDataProperty == null || materialsField == null || extrasField == null)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(InitializeWarningKey + ".Members", "Inventory Resource Stacks compatibility is missing one or more reflected members.");
        }
        else
        {
            StorageCompatLogger.LogDetectedBackend(Services.Stacking.StackingBackend.InventoryResourceStacks);
        }
    }

    internal static int GetContainerCount(ItemsContainer container, TechType techType)
    {
        var baseCount = container.GetCount(techType);
        var extras = GetExtras();
        if (extras == null)
        {
            return baseCount;
        }

        try
        {
            if (!extras.Contains(techType))
            {
                return baseCount;
            }

            var extraAmount = Convert.ToInt32(extras[techType]);
            if (extraAmount <= 0)
            {
                return baseCount;
            }

            return baseCount + extraAmount;
        }
        catch (Exception ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(GetContainerCountWarningKey, $"Inventory Resource Stacks count compatibility failed: {ex.Message}");
            return baseCount;
        }
    }

    internal static bool TryConsumeVirtualUnit(ItemsContainer container, TechType techType, int amount)
    {
        if (amount <= 0 || container != Inventory.main?.container)
        {
            return false;
        }

        var extras = GetExtras();
        if (extras == null)
        {
            return false;
        }

        try
        {
            if (!extras.Contains(techType))
            {
                return false;
            }

            var current = Convert.ToInt32(extras[techType]);
            if (current <= 0)
            {
                return false;
            }

            var consume = Mathf.Min(amount, current);
            extras[techType] = current - consume;
            return consume == amount;
        }
        catch (Exception ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(TryConsumeWarningKey, $"Inventory Resource Stacks virtual consumption failed: {ex.Message}");
            return false;
        }
    }

    internal static void TryResyncMaterializedItems(ItemsContainer inventoryContainer)
    {
        if (inventoryContainer == null || lastResyncFrame == Time.frameCount)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(ResyncWarningKey + ".SkippedFrame", "Skipped resync because inventoryContainer is null or already resynced this frame.");
            return;
        }

        lastResyncFrame = Time.frameCount;
        if (materializeMethod == null || materialsField == null)
        {
            return;
        }

        var extras = GetExtras();
        if (extras == null)
        {
            return;
        }

        var previousRefillingValue = false;
        var canSetRefilling = isRefillingField != null;
        try
        {
            if (canSetRefilling)
            {
                previousRefillingValue = (bool)isRefillingField.GetValue(null);
                isRefillingField.SetValue(null, true);
            }

            var materials = materialsField.GetValue(null) as IEnumerable;
            if (materials == null)
            {
                return;
            }

            foreach (var material in materials)
            {
                if (!(material is TechType techType) || !extras.Contains(techType))
                {
                    continue;
                }

                var extraAmount = Convert.ToInt32(extras[techType]);
                if (extraAmount <= 0 || inventoryContainer.GetCount(techType) > 0)
                {
                    continue;
                }

                // Throttle materialization per-techType to avoid rapid repeated spawns
                // that produce pickup sounds and UI popups when players hold down inputs.
                if (lastMaterializeAt.TryGetValue(techType, out var lastAt))
                {
                    if (Time.unscaledTime - lastAt < MaterializeCooldownSeconds)
                    {
                        var msg = $"Skipping materialize for {techType} due to cooldown (last at {lastAt}).";
                        StorageCompatLogger.LogCompatibilityWarningOnce(ResyncWarningKey + ".CooldownSkipped", msg);
                        StorageCompatFileLogger.LogWarning(msg);
                        continue;
                    }
                }

                try
                {
                    var startMsg = $"Materializing 1 unit of {techType} at time {Time.unscaledTime}.";
                    StorageCompatLogger.LogCompatibilityWarningOnce(ResyncWarningKey + ".MaterializeStart", startMsg);
                    StorageCompatFileLogger.LogInfo(startMsg);
                    materializeMethod.Invoke(null, new object[] { techType, 1 });
                    lastMaterializeAt[techType] = Time.unscaledTime;
                    var doneMsg = $"Materialized {techType} successfully.";
                    StorageCompatLogger.LogCompatibilityWarningOnce(ResyncWarningKey + ".MaterializeDone", doneMsg);
                    StorageCompatFileLogger.LogInfo(doneMsg);
                }
                catch (Exception ex)
                {
                    StorageCompatLogger.LogCompatibilityWarningOnce(ResyncWarningKey + ".Materialize", $"Inventory Resource Stacks materialize failed: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(ResyncWarningKey, $"Inventory Resource Stacks resync failed: {ex.Message}");
        }
        finally
        {
            if (canSetRefilling)
            {
                try
                {
                    isRefillingField.SetValue(null, previousRefillingValue);
                }
                catch (Exception ex)
                {
                    StorageCompatLogger.LogCompatibilityWarningOnce(RefillingWarningKey, $"Inventory Resource Stacks could not restore refilling state: {ex.Message}");
                }
            }
        }
    }

    private static IDictionary GetExtras()
    {
        if (mainSaveDataProperty == null || extrasField == null)
        {
            return null;
        }

        var saveData = mainSaveDataProperty.GetValue(null, null);
        if (saveData == null)
        {
            return null;
        }

        return extrasField.GetValue(saveData) as IDictionary;
    }
}
