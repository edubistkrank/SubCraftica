using System;
using BepInEx.Bootstrap;
using HarmonyLib;

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
            return false;
        }

        var type = client.GetType();
        var fullName = type.FullName;
        if (!string.IsNullOrEmpty(fullName) && fullName == AlienFabricatorTypeFullName)
        {
            return true;
        }

        return type.Name == "AlienFabricator" && type.Namespace == "PrototypeSubMod.Prefabs";
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

    public bool IsAlienFabricatorCrafting(object fabricator)
    {
        if (!IsPrototypeFabricator(fabricator))
        {
            return false;
        }

        try
        {
            var craftingField = AccessTools.Field(fabricator.GetType(), AlienFabricatorCraftingFieldName);
            if (craftingField == null)
            {
                return false;
            }

            var value = craftingField.GetValue(fabricator);
            return value is bool isCrafting && isCrafting;
        }
        catch
        {
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
}
