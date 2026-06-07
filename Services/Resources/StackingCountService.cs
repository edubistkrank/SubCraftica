using System;
using System.Reflection;
using SubCraftica.Patches.Compat;
using SubCraftica.Services.Stacking;

namespace SubCraftica.Services.Resources;

internal sealed class StackingCountService
{
    private const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private readonly StackingDetectionService stackingDetection;
    private MethodInfo totalStackUnitsMethod;

    public StackingCountService(StackingDetectionService stackingDetection)
    {
        this.stackingDetection = stackingDetection;
    }

    public void Initialize()
    {
        if (stackingDetection.Backend == StackingBackend.MadesRedoInventoryStacking)
        {
            totalStackUnitsMethod = MadesRedoInventoryStackingCompatPatch.ResolveTotalStackUnitsMethod();
            return;
        }

        if (stackingDetection.Backend != StackingBackend.InventoryResourceStacks)
        {
            return;
        }

        InventoryResourceStacksCompatPatch.Initialize();
    }

    public int GetContainerCount(ItemsContainer container, TechType techType)
    {
        if (container == null)
        {
            return 0;
        }

        if (stackingDetection.Backend != StackingBackend.MadesRedoInventoryStacking || totalStackUnitsMethod == null)
        {
            if (stackingDetection.Backend == StackingBackend.InventoryResourceStacks && container == Inventory.main?.container)
            {
                return InventoryResourceStacksCompatPatch.GetContainerCount(container, techType);
            }

            return container.GetCount(techType);
        }

        try
        {
            return (int)totalStackUnitsMethod.Invoke(null, new object[] { container, techType });
        }
        catch
        {
            return container.GetCount(techType);
        }
    }

    public bool TryConsumeVirtualUnit(ItemsContainer container, TechType techType, int amount = 1)
    {
        if (amount <= 0 || container == null)
        {
            return false;
        }

        if (stackingDetection.Backend != StackingBackend.InventoryResourceStacks || container != Inventory.main?.container)
        {
            return false;
        }

        return InventoryResourceStacksCompatPatch.TryConsumeVirtualUnit(container, techType, amount);
    }
}