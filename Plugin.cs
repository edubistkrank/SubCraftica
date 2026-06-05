using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Nautilus.Handlers;
using SubCraftica.Patches;
using SubCraftica.Services.Composition;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Localization;
using SubCraftica.Services.Resources;
using SubCraftica.Services.Stacking;
using SubCraftica.Services.UI;
using UnityEngine;

namespace SubCraftica;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("com.snmodding.nautilus", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("mades.redo.inventorystacking", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.Complot69.virtualstack", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.zerotheabsolute.powersaver", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("InferiusQoL", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(SubCraftica.Patches.Compat.VisibleLockerInteriorCompatPatch.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    private Harmony harmony;
    private float nextStackingRedetectAt;
    private bool stackingBackendLocked;

    internal static ManualLogSource Log { get; private set; }
    internal static ModServices Services { get; private set; }

    private void Awake()
    {
        Log = Logger;

        var config = ModConfig.Bind(Config);
        Services = new ModServices(config);
        RefreshStackingBackend("Awake");
        Services.InferiusQoLCompat?.Tick();

        OptionsPanelHandler.RegisterModOptions(new SubCrafticaOptions(config));

        GhostCrafterCraftPatch.Services = Services;

        harmony = new Harmony(PluginInfo.Guid);
        var failedPatchCount = PatchSafely(harmony);

        StorageCompatLogger.LogDetectedBackend(Services.StackingDetection.Backend);
        if (failedPatchCount > 0)
        {
            Log.LogWarning($"{PluginInfo.Name}: {failedPatchCount} patch(es) failed to apply. Review earlier log entries for details.");
        }

        Log.LogInfo($"{PluginInfo.Name} {PluginInfo.Version} loaded. Stacking backend: {Services.StackingDetection.Backend}");
    }

    private static int PatchSafely(Harmony harmonyInstance)
    {
        if (harmonyInstance == null)
        {
            throw new ArgumentNullException(nameof(harmonyInstance));
        }

        var failedPatchCount = 0;
        var patchTypes = typeof(Plugin).Assembly
            .GetTypes()
            .Where(type => type.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0);

        foreach (var patchType in patchTypes)
        {
            try
            {
                harmonyInstance.CreateClassProcessor(patchType).Patch();
            }
            catch (Exception ex)
            {
                failedPatchCount++;
                Log?.LogError($"Patch failed for {patchType.FullName}: {ex}");
            }
        }

        return failedPatchCount;
    }

    private void Update()
    {
        TryLateStackingRedetect();
        Services?.InferiusQoLCompat?.Tick();
        TryStopQueueHotkey();
        Services?.NearbyStorage?.Update();
        Services?.TimeController.Update();
        RecipeOwnedIngredientsTooltipService.Update();
    }

    private void TryStopQueueHotkey()
    {
        if (Services == null || !Input.GetKeyDown(KeyCode.Backspace))
        {
            return;
        }

        var hadQueuedItems = Services.Queue.Count > 0;
        var hadActiveCraft = Services.Synchronization.IsCraftInProgress;
        if (!hadQueuedItems && !hadActiveCraft)
        {
            return;
        }

        Services.Queue.Clear();
        Services.QueueFeedback.ClearAllProgress();
        Services.QueueCoordinator.RequestStopQueueContinuation(Services.Queue);

        ErrorMessage.AddMessage(ModText.Get(ModText.QueueStopped));
    }

    private static void RefreshStackingBackend(string source)
    {
        if (Services == null)
        {
            return;
        }

        Services.StackingDetection.Detect();
        Services.StackingCount.Initialize();
        Log?.LogInfo($"[StackingDetection/{source}] Backend detected: {Services.StackingDetection.Backend}");
    }

    private void TryLateStackingRedetect()
    {
        if (stackingBackendLocked || Services == null)
        {
            return;
        }

        if (Time.unscaledTime < nextStackingRedetectAt)
        {
            return;
        }

        nextStackingRedetectAt = Time.unscaledTime + 2f;
        var before = Services.StackingDetection.Backend;
        RefreshStackingBackend("Late");
        var after = Services.StackingDetection.Backend;

        if (after != StackingBackend.Vanilla || Time.unscaledTime > 12f)
        {
            stackingBackendLocked = true;
        }

        if (before != after)
        {
            Log?.LogInfo($"[StackingDetection] Backend switched {before} -> {after}");
        }
    }

    private void OnDestroy()
    {
        harmony?.UnpatchSelf();
    }
}