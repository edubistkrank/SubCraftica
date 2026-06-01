using System.Collections.Generic;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftPlanResult
{
    public CraftPlanResult(bool success, Dictionary<TechType, int> consumed, Dictionary<TechType, int> crafted)
    {
        Success = success;
        Consumed = consumed;
        Crafted = crafted;
    }

    public bool Success { get; }
    public Dictionary<TechType, int> Consumed { get; }
    public Dictionary<TechType, int> Crafted { get; }
}