using System;
using System.Collections.Generic;
using System.Linq;
using SubCraftica.Patches.Compat;
using SubCraftica.Services.Configuration;
using UnityEngine;

namespace SubCraftica.Services.Resources;

internal sealed class NearbyStorageService
{
    private const float DiscoveryIntervalSeconds = 2f;

    private readonly ModConfig config;
    private readonly StackingCountService stackingCount;
    private readonly Dictionary<Component, ItemsContainer> containers = new Dictionary<Component, ItemsContainer>();
    private readonly Dictionary<ItemsContainer, Component> containerOwners = new Dictionary<ItemsContainer, Component>();
    private readonly HashSet<Component> pendingVisibleLockerOwners = new HashSet<Component>();
    private bool pendingVisibleLockerRefreshAll;

    private float discoveryExpiresAt;

    public NearbyStorageService(ModConfig config, StackingCountService stackingCount)
    {
        this.config = config;
        this.stackingCount = stackingCount;
    }

    public Component GetOwner(ItemsContainer container)
    {
        if (container == null)
        {
            return null;
        }

        return containerOwners.TryGetValue(container, out var owner) ? owner : null;
    }

    public void Register(Component owner, ItemsContainer container)
    {
        if (owner == null || container == null)
        {
            return;
        }

        if (containers.TryGetValue(owner, out var previousContainer) && previousContainer != null)
        {
            containerOwners.Remove(previousContainer);
        }

        containers[owner] = container;
        containerOwners[container] = owner;
    }

    public void Unregister(Component owner)
    {
        if (owner == null)
        {
            return;
        }

        if (containers.TryGetValue(owner, out var container) && container != null)
        {
            containerOwners.Remove(container);
        }

        containers.Remove(owner);
    }

    public int GetNearbyCount(TechType techType, ItemsContainer excludedContainer)
    {
        if (config.StorageCraftMode.Value == ModConfig.StorageModeDisabled)
        {
            return 0;
        }

        return GetEligibleContainers(excludedContainer, includeExtractionExcluded: false)
            .Sum(container => stackingCount.GetContainerCount(container, techType));
    }

    public bool DestroyInNearby(TechType techType, int amount, ItemsContainer excludedContainer)
    {
        if (config.StorageCraftMode.Value == ModConfig.StorageModeDisabled || amount <= 0)
        {
            return false;
        }

        var remaining = amount;
        var changedOwners = new HashSet<Component>();
        foreach (var container in GetEligibleContainers(excludedContainer, includeExtractionExcluded: false))
        {
            var removedFromThisContainer = false;
            while (remaining > 0)
            {
                if (!container.DestroyItem(techType))
                {
                    break;
                }

                remaining--;
                removedFromThisContainer = true;
            }

            if (removedFromThisContainer)
            {
                var owner = GetOwner(container);
                if (owner != null)
                {
                    changedOwners.Add(owner);
                }
            }

            if (remaining == 0)
            {
                QueueVisibleLockerInteriorsRefresh(changedOwners);
                return true;
            }
        }

        QueueVisibleLockerInteriorsRefresh(changedOwners);

        return false;
    }

    public bool TryAddToNearbyStorage(Pickupable pickupable)
    {
        if (pickupable == null)
        {
            return false;
        }

        var preferredCandidates = new List<ItemsContainer>();
        var regularCandidates = new List<ItemsContainer>();

        foreach (var container in GetEligibleContainers(null, includeExtractionExcluded: true))
        {
            var owner = GetOwner(container);
            var storageId = StorageIdentifierResolver.GetStorageId(owner);
            if (Plugin.Services?.StoragePreferredSurplus?.IsPreferred(storageId) == true)
            {
                preferredCandidates.Add(container);
            }
            else
            {
                regularCandidates.Add(container);
            }
        }

        if (TryAddToCandidates(preferredCandidates, pickupable))
        {
            return true;
        }

        return TryAddToCandidates(regularCandidates, pickupable);
    }

    private bool TryAddToCandidates(IEnumerable<ItemsContainer> candidates, Pickupable pickupable)
    {
        if (candidates == null || pickupable == null)
        {
            return false;
        }

        foreach (var container in candidates)
        {
            if (container.AddItem(pickupable) != null)
            {
                QueueVisibleLockerInteriorsRefresh(new[] { GetOwner(container) });
                return true;
            }
        }

        return false;
    }

    private IEnumerable<ItemsContainer> GetEligibleContainers(ItemsContainer excludedContainer, bool includeExtractionExcluded)
    {
        if (Player.main == null)
        {
            return Enumerable.Empty<ItemsContainer>();
        }

        DiscoverContainersIfNeeded();

        var playerPosition = Player.main.transform.position;
        var useAllLoaded = config.StorageCraftMode.Value == ModConfig.StorageModeAllLoaded;
        var maxDistanceSquared = config.StorageRange.Value * config.StorageRange.Value;

        var uniqueContainers = new HashSet<ItemsContainer>();
        var entries = new List<KeyValuePair<ItemsContainer, float>>();

        foreach (var pair in containers.ToArray())
        {
            var owner = pair.Key;
            var container = pair.Value;

            if (owner == null || container == null)
            {
                if (container != null)
                {
                    containerOwners.Remove(container);
                }

                containers.Remove(owner);
                continue;
            }

            if (container == excludedContainer)
            {
                continue;
            }

            if (!IsContainerEligible(owner, container))
            {
                continue;
            }

            if (!includeExtractionExcluded)
            {
                var storageId = StorageIdentifierResolver.GetStorageId(owner);
                if (Plugin.Services?.StorageExtractionExclusions?.IsExcluded(storageId) == true)
                {
                    continue;
                }
            }

            if (!uniqueContainers.Add(container))
            {
                continue;
            }

            var distanceSquared = (owner.transform.position - playerPosition).sqrMagnitude;
            if (!useAllLoaded && distanceSquared > maxDistanceSquared)
            {
                continue;
            }

            entries.Add(new KeyValuePair<ItemsContainer, float>(container, distanceSquared));
        }

        return entries
            .OrderBy(entry => entry.Value)
            .Select(entry => entry.Key)
            .ToArray();
    }

    private static void TryUpdateVisibleLockerInterior(Component owner)
    {
        VisibleLockerInteriorCompatPatch.TryRefresh(owner);
    }

    internal void Update()
    {
        if (!pendingVisibleLockerRefreshAll && pendingVisibleLockerOwners.Count == 0)
        {
            return;
        }

        if (pendingVisibleLockerRefreshAll)
        {
            pendingVisibleLockerRefreshAll = false;
            pendingVisibleLockerOwners.Clear();

            foreach (var pair in containers)
            {
                TryUpdateVisibleLockerInterior(pair.Key);
            }

            return;
        }

        var owners = pendingVisibleLockerOwners.ToArray();
        pendingVisibleLockerOwners.Clear();
        for (var i = 0; i < owners.Length; i++)
        {
            TryUpdateVisibleLockerInterior(owners[i]);
        }
    }

    private void QueueVisibleLockerInteriorsRefresh(IEnumerable<Component> owners)
    {
        if (owners == null)
        {
            return;
        }

        var queuedAny = false;
        foreach (var owner in owners)
        {
            if (owner == null)
            {
                continue;
            }

            pendingVisibleLockerOwners.Add(owner);
            queuedAny = true;
        }

        if (queuedAny)
        {
            pendingVisibleLockerRefreshAll = pendingVisibleLockerOwners.Count == containers.Count;
        }
    }

    private void DiscoverContainersIfNeeded()
    {
        if (Time.time < discoveryExpiresAt)
        {
            return;
        }

        discoveryExpiresAt = Time.time + DiscoveryIntervalSeconds;

        foreach (var storage in UnityEngine.Object.FindObjectsOfType<StorageContainer>())
        {
            if (storage == null || storage.container == null)
            {
                continue;
            }

            Register(storage, storage.container);
        }
    }

    private static bool IsContainerEligible(Component owner, ItemsContainer container)
    {
        if (owner == null || container == null)
        {
            return false;
        }

        return true;
    }
}