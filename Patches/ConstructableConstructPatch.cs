using System;
using HarmonyLib;

namespace SubCraftica.Patches;

[HarmonyPatch(typeof(Constructable), "Construct")]
internal static class ConstructableConstructPatch
{
    [HarmonyPrefix]
    private static void Prefix()
    {
        ConstructableConstructPatchContext.IsActive = true;
    }

    [HarmonyFinalizer]
    private static void Finalizer(Exception __exception)
    {
        try
        {
            InventoryDestroyItemPatch.FlushConstructSurplus();
        }
        finally
        {
            ConstructableConstructPatchContext.IsActive = false;
        }
    }
}

internal static class ConstructableConstructPatchContext
{
    private static bool isActive;

    public static bool IsActive
    {
        get => isActive;
        set => isActive = value;
    }
}
