using System.Reflection;
using UnityEngine;

namespace SubCraftica.Services.Resources;

/// <summary>
/// Manages consumption of resources from equipped items (worn equipment) like tanks, upgrade modules, etc.
/// This is the last resort before attempting auto-crafting of sub-recipes.
/// </summary>
internal sealed class EquippedResourcesService
{
    private static MethodInfo _equipmentDestroyItemMethod;
    private static MethodInfo _equipmentGetCountMethod;

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

        var remaining = amount;
        while (remaining > 0)
        {
            if (!DestroyItemFromEquipment(equipment, techType))
            {
                break;
            }

            remaining--;
        }

        return remaining < amount; // Return true if we consumed at least 1
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

        return GetEquipmentCount(equipment, techType) > 0;
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

        return GetEquipmentCount(equipment, techType);
    }

    private static Equipment GetPlayerEquipment()
    {
        var player = Player.main;
        if (player == null)
        {
            return null;
        }

        var equipment = player.GetComponent<Equipment>();
        return equipment;
    }

    private static bool DestroyItemFromEquipment(Equipment equipment, TechType techType)
    {
        try
        {
            if (_equipmentDestroyItemMethod == null)
            {
                _equipmentDestroyItemMethod = equipment.GetType().GetMethod(
                    "DestroyItem",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(TechType) },
                    null);
            }

            if (_equipmentDestroyItemMethod == null)
            {
                return false;
            }

            var result = _equipmentDestroyItemMethod.Invoke(equipment, new object[] { techType });
            return result is bool success && success;
        }
        catch
        {
            return false;
        }
    }

    private static int GetEquipmentCount(Equipment equipment, TechType techType)
    {
        try
        {
            if (_equipmentGetCountMethod == null)
            {
                _equipmentGetCountMethod = equipment.GetType().GetMethod(
                    "GetCount",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(TechType) },
                    null);
            }

            if (_equipmentGetCountMethod == null)
            {
                return 0;
            }

            var result = _equipmentGetCountMethod.Invoke(equipment, new object[] { techType });
            return result is int count ? count : 0;
        }
        catch
        {
            return 0;
        }
    }
}
