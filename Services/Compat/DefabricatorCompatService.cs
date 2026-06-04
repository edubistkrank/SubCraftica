using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using UnityEngine;

namespace SubCraftica.Services.Compat;

internal sealed class DefabricatorCompatService
{
    private const string DefabricatorGuid = "com.mrpurple6411.Defabricator";
    private const string MainTypeFullName = "Defabricator.Main";
    private const string RecyclingDataTypeFullName = "Defabricator.RecyclingData";

    private readonly Type mainType;
    private readonly Type recyclingDataType;
    private readonly PropertyInfo activeProperty;
    private readonly FieldInfo reverseCacheField;

    public DefabricatorCompatService()
    {
        mainType = ResolveType(MainTypeFullName);
        recyclingDataType = ResolveType(RecyclingDataTypeFullName);
        activeProperty = mainType?.GetProperty("Active", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        reverseCacheField = recyclingDataType?.GetField("reverseCache", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
    }

    public bool IsInstalled => Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(DefabricatorGuid);

    public bool IsRecycleModeActive
    {
        get
        {
            if (!IsInstalled || activeProperty == null)
            {
                return false;
            }

            try
            {
                var value = activeProperty.GetValue(null, null);
                return value is bool active && active;
            }
            catch
            {
                return false;
            }
        }
    }

    public bool IsDefabricationActiveFor(TechType techType)
    {
        if (!IsRecycleModeActive || techType == TechType.None)
        {
            return false;
        }

        if (TryGetOriginTechType(techType, out _))
        {
            return true;
        }

        // Fallback: Defabricator custom entries are typically named Defabricated{OriginTech}
        var id = TechTypeExtensions.AsString(techType, false);
        return !string.IsNullOrWhiteSpace(id)
            && id.StartsWith("Defabricated", StringComparison.OrdinalIgnoreCase);
    }

    public bool TryGetOriginTechType(TechType recycleTechType, out TechType originTechType)
    {
        originTechType = TechType.None;
        if (recycleTechType == TechType.None || reverseCacheField == null)
        {
            return false;
        }

        try
        {
            var mapObject = reverseCacheField.GetValue(null);
            if (!(mapObject is IDictionary map))
            {
                return false;
            }

            if (!map.Contains(recycleTechType))
            {
                return false;
            }

            var value = map[recycleTechType];
            if (!(value is TechType origin) || origin == TechType.None)
            {
                return false;
            }

            originTechType = origin;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public int GetMaxRecyclableAmount(TechType recycleTechType)
    {
        if (!IsDefabricationActiveFor(recycleTechType) || Inventory.main == null)
        {
            return 0;
        }

        var ingredients = TechData.GetIngredients(recycleTechType);
        if (ingredients == null || ingredients.Count == 0)
        {
            return 0;
        }

        var maxAmount = int.MaxValue;
        for (var i = 0; i < ingredients.Count; i++)
        {
            var ingredient = ingredients[i];
            if (ingredient.techType == TechType.None || ingredient.amount <= 0)
            {
                continue;
            }

            var available = Inventory.main.GetPickupCount(ingredient.techType);
            var maxByIngredient = available / ingredient.amount;
            if (maxByIngredient < maxAmount)
            {
                maxAmount = maxByIngredient;
            }
        }

        return maxAmount == int.MaxValue ? 0 : Mathf.Max(0, maxAmount);
    }

    public bool CanRecycleAmount(TechType recycleTechType, int amount)
    {
        if (amount <= 0)
        {
            return false;
        }

        var max = GetMaxRecyclableAmount(recycleTechType);
        return max > 0 && amount <= max;
    }

    public List<TechType> GetRecycleLinkedItems(TechType recycleTechType)
    {
        var result = new List<TechType>();
        if (!IsDefabricationActiveFor(recycleTechType))
        {
            return result;
        }

        var linkedItems = TechData.GetLinkedItems(recycleTechType);
        if (linkedItems == null || linkedItems.Count == 0)
        {
            return result;
        }

        for (var i = 0; i < linkedItems.Count; i++)
        {
            var item = linkedItems[i];
            if (item != TechType.None)
            {
                result.Add(item);
            }
        }

        return result;
    }

    public string BuildRecycleOutputsText(TechType recycleTechType, int amount)
    {
        if (amount <= 0)
        {
            return string.Empty;
        }

        var sourceItems = GetRecycleLinkedItems(recycleTechType);
        if (sourceItems.Count == 0)
        {
            return string.Empty;
        }

        var counts = new Dictionary<TechType, int>();
        var ordered = new List<TechType>();
        for (var i = 0; i < sourceItems.Count; i++)
        {
            var item = sourceItems[i];
            if (!counts.ContainsKey(item))
            {
                counts[item] = 0;
                ordered.Add(item);
            }

            counts[item] += amount;
        }

        if (counts.Count == 0)
        {
            return string.Empty;
        }

        var parts = new List<string>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var techType = ordered[i];
            var name = Language.main != null
                ? Language.main.Get(TechTypeExtensions.AsString(techType, false))
                : TechTypeExtensions.AsString(techType, false);

            var value = counts[techType];
            parts.Add(value > 1 ? $"{name} x{value}" : name);
        }

        return string.Join(", ", parts.ToArray());
    }

    private static Type ResolveType(string fullName)
    {
        var direct = Type.GetType(fullName, throwOnError: false);
        if (direct != null)
        {
            return direct;
        }

        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            var type = assemblies[i].GetType(fullName, throwOnError: false, ignoreCase: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }
}
