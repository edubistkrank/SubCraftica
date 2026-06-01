using SubCraftica.Services.Configuration;
using UnityEngine;

namespace SubCraftica.Services.Crafting;

internal sealed class QuantitySelectionService
{
    private readonly ModConfig config;
    private readonly RecipePlannerService planner;

    private TechType currentTechType = TechType.None;
    private int currentAmount = 1;

    public QuantitySelectionService(ModConfig config, RecipePlannerService planner)
    {
        this.config = config;
        this.planner = planner;
    }

    public int GetCurrentAmount(TechType techType)
    {
        if (currentTechType != techType)
        {
            currentTechType = techType;
            currentAmount = 1;
        }

        return currentAmount;
    }

    public int UpdateWithScroll(TechType techType)
    {
        if (currentTechType != techType)
        {
            currentTechType = techType;
            currentAmount = 1;
        }

        var scrollDelta = Input.mouseScrollDelta.y;
        if (scrollDelta > 0.01f)
        {
            TryIncrease(techType);
        }
        else if (scrollDelta < -0.01f)
        {
            currentAmount = Mathf.Max(1, currentAmount - 1);
        }

        return currentAmount;
    }

    private void TryIncrease(TechType techType)
    {
        var upperLimit = Mathf.Max(1, config.MaxQueueSize.Value);
        if (currentAmount >= upperLimit)
        {
            return;
        }

        var candidate = currentAmount + 1;
        var plan = planner.BuildRequestPlan(techType, candidate);
        if (plan.Success)
        {
            currentAmount = candidate;
        }
    }
}