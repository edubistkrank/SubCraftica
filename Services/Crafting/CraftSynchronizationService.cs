namespace SubCraftica.Services.Crafting;

internal sealed class CraftSynchronizationService
{
    private int activeCraftCount;

    public bool TryEnterCraft()
    {
        if (activeCraftCount > 0)
        {
            return false;
        }

        activeCraftCount++;
        return true;
    }

    public void ExitCraft()
    {
        if (activeCraftCount > 0)
        {
            activeCraftCount--;
        }
    }

    public bool IsCraftInProgress => activeCraftCount > 0;
}
