using System;
using System.Reflection;
using UnityEngine;
using HarmonyLib;

namespace SubCraftica.Services.Resources;

internal static class StorageIdentifierResolver
{
    private static readonly Type UniqueIdentifierType = AccessType("UniqueIdentifier");
    private static readonly Type PrefabIdentifierType = AccessType("PrefabIdentifier");

    private static readonly PropertyInfo UniqueIdProperty = UniqueIdentifierType?.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo PrefabIdProperty = PrefabIdentifierType?.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo UniqueClassIdProperty = UniqueIdentifierType?.GetProperty("ClassId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly PropertyInfo PrefabClassIdProperty = PrefabIdentifierType?.GetProperty("ClassId", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    public static string GetStorageId(Component owner)
    {
        if (owner == null)
        {
            return null;
        }

        var uniqueId = TryGetStringProperty(owner, UniqueIdentifierType, UniqueIdProperty);
        if (!string.IsNullOrWhiteSpace(uniqueId))
        {
            return uniqueId;
        }

        var prefabId = TryGetStringProperty(owner, PrefabIdentifierType, PrefabIdProperty);
        if (!string.IsNullOrWhiteSpace(prefabId))
        {
            return prefabId;
        }

        var uniqueClassId = TryGetStringProperty(owner, UniqueIdentifierType, UniqueClassIdProperty);
        if (!string.IsNullOrWhiteSpace(uniqueClassId))
        {
            return uniqueClassId;
        }

        var prefabClassId = TryGetStringProperty(owner, PrefabIdentifierType, PrefabClassIdProperty);
        if (!string.IsNullOrWhiteSpace(prefabClassId))
        {
            return prefabClassId;
        }

        return owner.transform != null ? owner.transform.GetHierarchyPath() : owner.GetInstanceID().ToString();
    }

    private static string TryGetStringProperty(Component owner, Type componentType, PropertyInfo property)
    {
        if (componentType == null || property == null || owner == null || owner.gameObject == null)
        {
            return null;
        }

        var component = owner.gameObject.GetComponent(componentType);
        if (component == null)
        {
            return null;
        }

        return property.GetValue(component, null) as string;
    }

    private static Type AccessType(string typeName)
    {
        var type = typeof(Inventory).Assembly.GetType(typeName, throwOnError: false);
        if (type != null)
        {
            return type;
        }

        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            type = asm.GetType(typeName, throwOnError: false);
            if (type != null)
            {
                return type;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(this Transform transform)
    {
        if (transform == null)
        {
            return string.Empty;
        }

        var path = transform.name;
        var current = transform.parent;
        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
