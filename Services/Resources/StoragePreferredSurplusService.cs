using System;
using System.Collections.Generic;
using System.Linq;
using SubCraftica.Services.Configuration;

namespace SubCraftica.Services.Resources;

internal sealed class StoragePreferredSurplusService
{
    private const char Separator = ';';

    private readonly ModConfig config;
    private readonly HashSet<string> preferredIds = new HashSet<string>(StringComparer.Ordinal);

    public StoragePreferredSurplusService(ModConfig config)
    {
        this.config = config;
        LoadFromConfig();
    }

    public bool IsPreferred(string id)
    {
        return !string.IsNullOrWhiteSpace(id) && preferredIds.Contains(id);
    }

    public bool SetPreferred(string id, bool preferred)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        var changed = preferred ? preferredIds.Add(id) : preferredIds.Remove(id);
        if (!changed)
        {
            return false;
        }

        SaveToConfig();
        return true;
    }

    private void LoadFromConfig()
    {
        preferredIds.Clear();

        if (config?.PreferredSurplusStorageIds == null || string.IsNullOrWhiteSpace(config.PreferredSurplusStorageIds.Value))
        {
            return;
        }

        var parts = config.PreferredSurplusStorageIds.Value
            .Split(new[] { Separator }, StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part));

        foreach (var part in parts)
        {
            preferredIds.Add(part);
        }
    }

    private void SaveToConfig()
    {
        if (config?.PreferredSurplusStorageIds == null)
        {
            return;
        }

        config.PreferredSurplusStorageIds.Value = string.Join(Separator.ToString(), preferredIds.OrderBy(static x => x, StringComparer.Ordinal));
    }
}
