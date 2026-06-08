using System;
using BepInEx.Bootstrap;

namespace SubCraftica.Services.Compat;

internal sealed class PrototypeSubCompatService
{
    internal const string PluginGuid = "com.prototech.prototypesub";
    private const string AlienFabricatorTypeFullName = "PrototypeSubMod.Prefabs.AlienFabricator";

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
}
