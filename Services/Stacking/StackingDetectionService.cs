using System;
using System.Linq;
using BepInEx.Bootstrap;
using SubCraftica.Patches.Compat;

namespace SubCraftica.Services.Stacking;

internal sealed class StackingDetectionService
{
    public StackingBackend Backend { get; private set; } = StackingBackend.Vanilla;

    public void Detect()
    {
        if (Chainloader.PluginInfos.ContainsKey(MadesRedoInventoryStackingCompatPatch.PluginGuid)
            || IsAssemblyLoaded(MadesRedoInventoryStackingCompatPatch.AssemblyName)
            || IsTypeLoaded(MadesRedoInventoryStackingCompatPatch.StackTypeName))
        {
            Backend = StackingBackend.MadesRedoInventoryStacking;
            return;
        }

        if (Chainloader.PluginInfos.ContainsKey(InventoryResourceStacksCompatPatch.PluginGuid)
            || IsAssemblyLoaded(InventoryResourceStacksCompatPatch.AssemblyName)
            || IsTypeLoaded(InventoryResourceStacksCompatPatch.PluginTypeName))
        {
            Backend = StackingBackend.InventoryResourceStacks;
            return;
        }

        Backend = StackingBackend.Vanilla;
    }

    private static bool IsAssemblyLoaded(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies().Any(assembly =>
            string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsTypeLoaded(string fullTypeName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType(fullTypeName, false))
            .Any(type => type != null);
    }
}