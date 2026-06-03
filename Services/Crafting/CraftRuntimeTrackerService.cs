namespace SubCraftica.Services.Crafting;

internal sealed class CraftRuntimeTrackerService
{
    private TechType lastTechType = TechType.None;
    private TechType lastPerItemFinished = TechType.None;

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

    /// <summary>
    /// Called at per-item craft start so the finished techType survives until OnCraftingEnd.
    /// </summary>
    public void SetLastPerItemFinished(TechType techType)
    {
        lastPerItemFinished = techType;
    }

    /// <summary>
    /// Reads and clears the stored finished techType. Returns TechType.None if not set.
    /// </summary>
    public TechType ConsumeLastPerItemFinished()
    {
        var t = lastPerItemFinished;
        lastPerItemFinished = TechType.None;
        return t;
    }
}