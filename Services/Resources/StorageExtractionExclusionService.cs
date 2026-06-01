using System;
using System.Collections.Generic;
using System.Linq;
using SubCraftica.Services.Configuration;

namespace SubCraftica.Services.Resources;

internal sealed class StorageExtractionExclusionService
{
    private const char Separator = ';';

    private readonly ModConfig config;
    private readonly HashSet<string> excludedIds = new HashSet<string>(StringComparer.Ordinal);

    public StorageExtractionExclusionService(ModConfig config)
    {
        this.config = config;
        LoadFromConfig();
    }

    public bool IsExcluded(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && excludedIds.Contains(id);
    }

    public bool SetExcluded(string id, bool excluded)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var changed = excluded ? excludedIds.Add(id) : excludedIds.Remove(id);
        if (!changed)
        {
            return false;
        }

        SaveToConfig();
        return true;
    }

    private void LoadFromConfig()
    {
        excludedIds.Clear();

        if (config?.ExtractionExcludedStorageIds == null || string.IsNullOrWhiteSpace(config.ExtractionExcludedStorageIds.Value))
        {
            return;
        }

        var parts = config.ExtractionExcludedStorageIds.Value
            .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));

        foreach (var part in parts)
        {
            excludedIds.Add(part);
        }
    }

    private void SaveToConfig()
    {
        if (config?.ExtractionExcludedStorageIds == null)
        {
            return;
        }

        config.ExtractionExcludedStorageIds.Value = string.Join(Separator.ToString(), excludedIds.OrderBy(static x => x, StringComparer.Ordinal));
    }
}
