using System;
using System.Reflection;
using SubCraftica.Services.Resources;

namespace SubCraftica.Patches.Compat;

internal static class MadesRedoInventoryStackingCompatPatch
{
    private const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    private const string ResolveWarningKey = "MadesRedoInventoryStacking.ResolveTotalStackUnits";

    internal const string PluginGuid = "mades.redo.inventorystacking";
    internal const string AssemblyName = "MR_InventoryStacking";
    internal const string StackTypeName = "MR_InventoryStacking.MRStack";

    internal static MethodInfo ResolveTotalStackUnitsMethod()
    {
        var type = CompatReflectionHelper.FindType(StackTypeName, AssemblyName);
        if (type == null)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(ResolveWarningKey + ".Type", $"Could not find Mades Redo Inventory Stacking type '{StackTypeName}'.");
            return null;
        }

        var method = CompatReflectionHelper.FindStaticMethod(type, "TotalStackUnits", new[] { typeof(ItemsContainer), typeof(TechType) }, AllFlags);
        if (method == null)
        {
            StorageCompatLogger.LogCompatibilityWarningOnce(ResolveWarningKey + ".Method", "Could not resolve Mades Redo Inventory Stacking method 'TotalStackUnits'.");
        }

        return method;
    }
}

internal static class CompatReflectionHelper
{
    internal static Type FindTypeInLoadedAssemblies(string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(fullTypeName))
        {
            return null;
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType(fullTypeName, false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    internal static Type FindType(string fullTypeName, string assemblyName)
    {
        var type = FindTypeInLoadedAssemblies(fullTypeName);
        if (type != null || string.IsNullOrWhiteSpace(assemblyName))
        {
            return type;
        }

        return Type.GetType($"{fullTypeName}, {assemblyName}");
    }

    internal static MethodInfo FindStaticMethod(Type type, string methodName, Type[] parameterTypes, BindingFlags bindingFlags)
    {
        if (type == null || string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        return type.GetMethod(methodName, bindingFlags, null, parameterTypes, null);
    }
}
