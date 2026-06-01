using System;
using SubCraftica.Services.Configuration;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftingMathService
{
    private readonly ModConfig config;

    public CraftingMathService(ModConfig config)
    {
        this.config = config;
    }

    public float GetEnergy(float baseEnergy, int amount)
    {
        if (amount <= 0)
        {
            return 0f;
        }

        var multiplier = Math.Max(0f, config.CraftEnergyMultiplier.Value);
        return baseEnergy * amount * multiplier;
    }
}