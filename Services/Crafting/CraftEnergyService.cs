using System;
using System.Collections.Generic;
using SubCraftica.Services.Configuration;

namespace SubCraftica.Services.Crafting;

internal sealed class CraftEnergyService
{
    private readonly ModConfig config;
    private readonly CraftingMathService math;

    public CraftEnergyService(ModConfig config, CraftingMathService math)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.math = math ?? throw new ArgumentNullException(nameof(math));
    }

    public float GetRequiredEnergy(TechType techType, int amount)
    {
        if (amount <= 0)
        {
            return 0f;
        }

        var baseEnergy = GetBaseEnergy(techType);
        return math.GetEnergy(baseEnergy, amount);
    }

    public float GetRequiredEnergy(TechType finalTechType, Dictionary<TechType, int> crafted)
    {
        if (!config.IncludeSubrecipeEnergyCost.Value)
        {
            return GetFinalOnlyRequiredEnergy(finalTechType, crafted);
        }

        if (crafted == null || crafted.Count == 0)
        {
            return GetRequiredEnergy(finalTechType, 1);
        }

        var required = 0f;
        foreach (var pair in crafted)
        {
            if (pair.Key == TechType.None || pair.Value <= 0)
            {
                continue;
            }

            var outputPerCraft = GetEffectiveOutputPerCraft(pair.Key);
            var craftOperations = (int)Math.Ceiling(pair.Value / (float)outputPerCraft);
            if (craftOperations <= 0)
            {
                continue;
            }

            required += GetRequiredEnergy(pair.Key, craftOperations);
        }

        return required;
    }

    public bool HasEnoughEnergy(PowerRelay powerRelay, TechType finalTechType, float requiredTotalEnergy)
    {
        if (powerRelay == null || !GameModeUtils.RequiresPower())
        {
            return true;
        }

        return powerRelay.GetPower() >= requiredTotalEnergy;
    }

    public bool TryConsumePlannedEnergy(PowerRelay powerRelay, TechType finalTechType, float requiredTotalEnergy)
    {
        if (powerRelay == null || !GameModeUtils.RequiresPower())
        {
            return true;
        }

        var vanillaEnergy = GetBaseEnergy(finalTechType);
        var deltaEnergy = requiredTotalEnergy - vanillaEnergy;

        if (deltaEnergy > 0f)
        {
            if (powerRelay.GetPower() < deltaEnergy)
            {
                return false;
            }

            CrafterLogic.ConsumeEnergy(powerRelay, deltaEnergy);
            return true;
        }

        if (deltaEnergy < 0f && powerRelay is IPowerInterface relayInterface)
        {
            float amountAdded;
            PowerSystem.AddEnergy(relayInterface, -deltaEnergy, out amountAdded);
        }

        return true;
    }

    private float GetFinalOnlyRequiredEnergy(TechType finalTechType, Dictionary<TechType, int> crafted)
    {
        if (crafted == null || !crafted.TryGetValue(finalTechType, out var craftedFinalUnits) || craftedFinalUnits <= 0)
        {
            return GetRequiredEnergy(finalTechType, 1);
        }

        var outputPerCraft = GetEffectiveOutputPerCraft(finalTechType);
        var craftOperations = (int)Math.Ceiling(craftedFinalUnits / (float)outputPerCraft);
        return GetRequiredEnergy(finalTechType, craftOperations <= 0 ? 1 : craftOperations);
    }

    private static int GetBaseCraftAmount(TechType techType)
    {
        var amount = TechData.GetCraftAmount(techType);
        return amount <= 0 ? 1 : amount;
    }

    private static int GetEffectiveOutputPerCraft(TechType techType)
    {
        var output = GetBaseCraftAmount(techType);
        var linkedItems = TechData.GetLinkedItems(techType);
        if (linkedItems == null || linkedItems.Count == 0)
        {
            return output;
        }

        var sameTypeLinked = 0;
        for (var i = 0; i < linkedItems.Count; i++)
        {
            if (linkedItems[i] == techType)
            {
                sameTypeLinked++;
            }
        }

        return output + sameTypeLinked;
    }

    private static float GetBaseEnergy(TechType techType)
    {
        if (TechData.GetEnergyCost(techType, out var value) && value > 0f)
        {
            return value;
        }

        return 5f;
    }
}