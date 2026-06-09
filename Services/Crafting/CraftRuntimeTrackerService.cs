using System.Collections.Generic;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftRuntimeTrackerService
{
    private TechType lastTechType = TechType.None;
    private readonly Queue<TechType> perItemFinishedQueue = new Queue<TechType>();

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
    /// Called at per-item craft start so each completed craft can be matched later in OnCraftingEnd,
    /// even if another craft starts before the previous end event arrives.
    /// </summary>
    public void SetLastPerItemFinished(TechType techType)
    {
        if (techType == TechType.None)
        {
            return;
        }

        perItemFinishedQueue.Enqueue(techType);
    }

    /// <summary>
    /// Consumes one finished techType in FIFO order. Returns TechType.None if queue is empty.
    /// </summary>
    public TechType ConsumeLastPerItemFinished()
    {
        return perItemFinishedQueue.Count > 0 ? perItemFinishedQueue.Dequeue() : TechType.None;
    }
}
