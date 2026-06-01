namespace SubCraftica.Services.Crafting;

internal sealed class CraftRuntimeTrackerService
{
    private TechType lastTechType = TechType.None;

    public void SetLastTechType(TechType techType)
    {
        lastTechType = techType;
    }

    public bool TryGetLastTechType(out TechType techType)
    {
        techType = lastTechType;
        return techType != TechType.None;
    }

    public void Clear(TechType techType)
    {
        if (lastTechType == techType)
        {
            lastTechType = TechType.None;
        }
    }
}