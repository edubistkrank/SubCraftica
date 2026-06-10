using UnityEngine;

namespace SubCraftica.Services.Resources;

/// <summary>
/// Manages consumption of resources from equipped items (worn equipment) like tanks, upgrade modules, etc.
/// This is the last resort before attempting auto-crafting of sub-recipes.
/// </summary>
internal sealed class EquippedResourcesService
{
    /// <summary>
    /// Attempts to consume an item from player's equipped equipment.
    /// </summary>
    /// <param name="techType">The tech type to consume</param>
    /// <param name="amount">Amount to consume (typically 1)</param>
    /// <returns>True if the item was successfully consumed, false otherwise</returns>
    public bool TryConsumeFromEquipment(TechType techType, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        var equipment = GetPlayerEquipment();
        if (equipment == null)
        {
            return false;
        }

        var consumed = 0;

        while (consumed < amount)
        {
            if (!TryConsumeSingleFromEquipment(equipment, techType))
            {
                break;
            }

            consumed++;
        }

        return consumed > 0;
    }

    /// <summary>
    /// Checks if a specific tech type is currently equipped by the player.
    /// </summary>
    /// <param name="techType">The tech type to check</param>
    /// <returns>True if the item is equipped, false otherwise</returns>
    public bool IsEquipped(TechType techType)
    {
        var equipment = GetPlayerEquipment();
        if (equipment == null)
        {
            return false;
        }

        return equipment.GetCount(techType) > 0;
    }

    /// <summary>
    /// Gets the count of a specific equipped item type.
    /// Note: Typically 0 or 1 for equipped items, but included for consistency.
    /// </summary>
    /// <param name="techType">The tech type to count</param>
    /// <returns>0 or 1 if equipped, 0 otherwise</returns>
    public int GetEquippedCount(TechType techType)
    {
        var equipment = GetPlayerEquipment();
        if (equipment == null)
        {
            return 0;
        }

        return equipment.GetCount(techType);
    }

    private static Equipment GetPlayerEquipment()
    {
        return Inventory.main != null ? Inventory.main.equipment : null;
    }

    private static bool TryConsumeSingleFromEquipment(Equipment equipment, TechType techType)
    {
        foreach (EquipmentType equipmentType in System.Enum.GetValues(typeof(EquipmentType)))
        {
            var slots = new System.Collections.Generic.List<string>();
            equipment.GetSlots(equipmentType, slots);

            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (equipment.GetTechTypeInSlot(slot) != techType)
                {
                    continue;
                }

                var removed = equipment.RemoveItem(slot, true, false);
                if (removed?.item == null)
                {
                    return false;
                }

                Object.Destroy(removed.item.gameObject);
                return true;
            }
        }

        return false;
    }
}
