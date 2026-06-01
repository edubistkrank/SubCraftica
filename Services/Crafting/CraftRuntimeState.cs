using System.Collections.Generic;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftRuntimeState
{
    private readonly Dictionary<TechType, float> activeRequiredEnergy = new Dictionary<TechType, float>();

    public void SetRequiredEnergy(TechType techType, float requiredEnergy)
    {
        if (techType == TechType.None)
        {
            return;
        }

        if (requiredEnergy < 0f)
        {
            activeRequiredEnergy.Remove(techType);
            return;
        }

        activeRequiredEnergy[techType] = requiredEnergy;
    }

    public bool TryGetRequiredEnergy(TechType techType, out float requiredEnergy)
    {
        if (techType == TechType.None)
        {
            requiredEnergy = 0f;
            return false;
        }

        return activeRequiredEnergy.TryGetValue(techType, out requiredEnergy);
    }

    public float GetRequiredEnergy(TechType techType)
    {
        return TryGetRequiredEnergy(techType, out var requiredEnergy) ? requiredEnergy : 0f;
    }

    public void Clear(TechType techType)
    {
        if (techType == TechType.None)
        {
            return;
        }

        activeRequiredEnergy.Remove(techType);
    }
}