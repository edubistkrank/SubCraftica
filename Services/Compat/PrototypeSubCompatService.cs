using System;
using BepInEx.Bootstrap;
using HarmonyLib;
using SubCraftica.Services.Composition;
using SubCraftica.Services.Configuration;
using SubCraftica.Services.Logging;

namespace SubCraftica.Services.Compat;

internal sealed class PrototypeSubCompatService
{
    internal const string PluginGuid = "com.prototech.prototypesub";
    private const string AlienFabricatorTypeFullName = "PrototypeSubMod.Prefabs.AlienFabricator";
    private const string AlienFabricatorCraftingFieldName = "crafting";

    public bool IsInstalled => Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(PluginGuid);

    public bool IsPrototypeFabricator(object client)
    {
        if (!IsInstalled || client == null)
        {
            PrototypeCompatDebugLogger.Debug($"IsPrototypeFabricator => false (installed={IsInstalled}, clientNull={client == null})");
            return false;
        }

        var type = client.GetType();
        var fullName = type.FullName;
        if (!string.IsNullOrEmpty(fullName) && fullName == AlienFabricatorTypeFullName)
        {
            PrototypeCompatDebugLogger.Debug($"IsPrototypeFabricator => true (fullName={fullName})");
            return true;
        }

        var fallbackMatch = type.Name == "AlienFabricator" && type.Namespace == "PrototypeSubMod.Prefabs";
        PrototypeCompatDebugLogger.Debug($"IsPrototypeFabricator fallback => {fallbackMatch} (name={type.Name}, ns={type.Namespace ?? "null"})");
        return fallbackMatch;
    }

    public bool IsPrototypeCrafterClient(object client)
    {
        if (!IsInstalled || client == null)
        {
            return false;
        }

        if (IsPrototypeFabricator(client))
        {
            return true;
        }

        return client is Crafter && client.GetType().Namespace != null && client.GetType().Namespace.StartsWith("PrototypeSubMod", StringComparison.Ordinal);
    }

    public bool IsPrototypeFabricator(GhostCrafter crafter)
    {
        return IsPrototypeFabricator(crafter as object);
    }

    public bool IsPrototypeFabricatorClientActive()
    {
        var menu = uGUI.main != null ? uGUI.main.craftingMenu : null;
        return IsPrototypeFabricator(menu != null ? menu.client : null);
    }

    public bool IsPrototypeCrafterClientActive()
    {
        var menu = uGUI.main != null ? uGUI.main.craftingMenu : null;
        return IsPrototypeCrafterClient(menu != null ? menu.client : null);
    }

    public bool IsAlienFabricatorCrafting(object fabricator)
    {
        if (!IsPrototypeFabricator(fabricator))
        {
            PrototypeCompatDebugLogger.Debug("IsAlienFabricatorCrafting => false (not prototype fabricator)");
            return false;
        }

        try
        {
            var craftingField = AccessTools.Field(fabricator.GetType(), AlienFabricatorCraftingFieldName);
            if (craftingField == null)
            {
                PrototypeCompatDebugLogger.Warn("IsAlienFabricatorCrafting => false (crafting field not found)");
                return false;
            }

            var value = craftingField.GetValue(fabricator);
            var isCrafting = value is bool b && b;
            PrototypeCompatDebugLogger.Debug($"IsAlienFabricatorCrafting => {isCrafting}");
            return isCrafting;
        }
        catch (Exception ex)
        {
            PrototypeCompatDebugLogger.Error(ex, "IsAlienFabricatorCrafting failed");
            return false;
        }
    }

    public bool IsAlienFabricatorCrafting(GhostCrafter crafter)
    {
        return IsAlienFabricatorCrafting(crafter as object);
    }

    public bool IsAlienFabricatorCrafting(uGUI_CraftingMenu menu)
    {
        return IsAlienFabricatorCrafting(menu != null ? menu.client : null);
    }

    public bool ShouldBypassMainCraftPrefix(GhostCrafter crafter, int craftingMode)
    {
        return craftingMode == ModConfig.CraftingModePerItem && IsPrototypeFabricator(crafter);
    }

    public bool ShouldDeferCraftCleanup(GhostCrafter crafter, int craftingMode)
    {
        return craftingMode != ModConfig.CraftingModePerItem && IsPrototypeFabricator(crafter);
    }

    public void TryCleanupAfterCraftingEnd(GhostCrafter crafter, ModServices services)
    {
        if (services == null || !ShouldDeferCraftCleanup(crafter, services.Config.CraftingMode.Value))
        {
            PrototypeCompatDebugLogger.Debug("TryCleanupAfterCraftingEnd skipped");
            return;
        }

        if (!services.Runtime.TryGetLastTechType(out var lastTechType))
        {
            PrototypeCompatDebugLogger.Warn("TryCleanupAfterCraftingEnd skipped (no last tech type)");
            return;
        }

        PrototypeCompatDebugLogger.Info($"TryCleanupAfterCraftingEnd restoring state for {lastTechType}");

        services.RecipeOverride.Restore(lastTechType);
        services.Runtime.Clear(lastTechType);
        services.CraftRuntimeState.Clear(lastTechType);
    }
}
