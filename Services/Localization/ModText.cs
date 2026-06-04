using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace SubCraftica.Services.Localization;

internal static class ModText
{
    public const string CraftInProgress = "SubCraftica.CraftInProgress";
    public const string CraftAlreadyRunning = "SubCraftica.CraftAlreadyRunning";
    public const string QueueQueued = "SubCraftica.QueueQueued";
    public const string QueueProgress = "SubCraftica.QueueProgress";
    public const string QueueFull = "SubCraftica.QueueFull";
    public const string QueueMismatch = "SubCraftica.QueueMismatch";
    public const string QueueCompleted = "SubCraftica.QueueCompleted";
    public const string QueueStopped = "SubCraftica.QueueStopped";
    public const string WarningNotEnoughPower = "SubCraftica.WarningNotEnoughPower";
    public const string WarningConstructAutoCraftFailed = "SubCraftica.WarningConstructAutoCraftFailed";
    public const string WarningInventoryMovedToStorage = "SubCraftica.WarningInventoryMovedToStorage";
    public const string WarningNoSpaceInventoryAndStorage = "SubCraftica.WarningNoSpaceInventoryAndStorage";
    public const string ToggleExcludeFromExtraction = "SubCraftica.ToggleExcludeFromExtraction";
    public const string TogglePreferredForSurplus = "SubCraftica.TogglePreferredForSurplus";
    public const string ToggleLabelExclude = "SubCraftica.ToggleLabelExclude";
    public const string ToggleLabelPrefer = "SubCraftica.ToggleLabelPrefer";
    public const string Opt_EnableAutoSubcraft = "SubCraftica.Opt_EnableAutoSubcraft";
    public const string Opt_CreativeMode = "SubCraftica.Opt_CreativeMode";
    public const string Opt_CreativeMode_Disabled = "SubCraftica.Opt_CreativeMode_Disabled";
    public const string Opt_CreativeMode_LetsGo = "SubCraftica.Opt_CreativeMode_LetsGo";
    public const string Opt_CraftingTooltipMode = "SubCraftica.Opt_CraftingTooltipMode";
    public const string Opt_StorageMode = "SubCraftica.Opt_StorageMode";
    public const string Opt_StorageRange = "SubCraftica.Opt_StorageRange";
    public const string Opt_StorageOnlyIngredientColor = "SubCraftica.Opt_StorageOnlyIngredientColor";
    public const string Opt_InventoryIngredientColor = "SubCraftica.Opt_InventoryIngredientColor";
    public const string Opt_MissingIngredientColor = "SubCraftica.Opt_MissingIngredientColor";
    public const string Opt_MaxUnitsPerRequest = "SubCraftica.Opt_MaxUnitsPerRequest";
    public const string Opt_CraftingMode = "SubCraftica.Opt_CraftingMode";
    public const string Opt_EnergyMultiplier = "SubCraftica.Opt_EnergyMultiplier";
    public const string Opt_IncludeSubrecipeEnergy = "SubCraftica.Opt_IncludeSubrecipeEnergy";
    public const string Opt_ReturnSurplusToStorage = "SubCraftica.Opt_ReturnSurplusToStorage";
    public const string Opt_BlacklistToggle = "SubCraftica.Opt_BlacklistToggle";
    public const string Opt_SurplusToggle = "SubCraftica.Opt_SurplusToggle";
    public const string Opt_ResetToDefaults = "SubCraftica.Opt_ResetToDefaults";
    public const string OptGroup_Crafting = "SubCraftica.OptGroup_Crafting";
    public const string OptGroup_Storage = "SubCraftica.OptGroup_Storage";
    public const string OptGroup_Energy = "SubCraftica.OptGroup_Energy";
    public const string OptGroup_Colors = "SubCraftica.OptGroup_Colors";
    public const string OptGroup_AreYouSure = "SubCraftica.OptGroup_AreYouSure";
    public const string OptDesc_EnableAutoSubcraft = "SubCraftica.OptDesc_EnableAutoSubcraft";
    public const string OptDesc_CreativeMode = "SubCraftica.OptDesc_CreativeMode";
    public const string OptDesc_CraftingTooltipMode = "SubCraftica.OptDesc_CraftingTooltipMode";
    public const string OptDesc_StorageMode = "SubCraftica.OptDesc_StorageMode";
    public const string OptDesc_StorageRange = "SubCraftica.OptDesc_StorageRange";
    public const string OptDesc_StorageOnlyIngredientColor = "SubCraftica.OptDesc_StorageOnlyIngredientColor";
    public const string OptDesc_InventoryIngredientColor = "SubCraftica.OptDesc_InventoryIngredientColor";
    public const string OptDesc_MissingIngredientColor = "SubCraftica.OptDesc_MissingIngredientColor";
    public const string OptDesc_MaxUnitsPerRequest = "SubCraftica.OptDesc_MaxUnitsPerRequest";
    public const string OptDesc_CraftingMode = "SubCraftica.OptDesc_CraftingMode";
    public const string OptDesc_EnergyMultiplier = "SubCraftica.OptDesc_EnergyMultiplier";
    public const string OptDesc_IncludeSubrecipeEnergy = "SubCraftica.OptDesc_IncludeSubrecipeEnergy";
    public const string OptDesc_ReturnSurplusToStorage = "SubCraftica.OptDesc_ReturnSurplusToStorage";
    public const string OptDesc_BlacklistToggle = "SubCraftica.OptDesc_BlacklistToggle";
    public const string OptDesc_SurplusToggle = "SubCraftica.OptDesc_SurplusToggle";
    public const string OptDesc_ResetToDefaults = "SubCraftica.OptDesc_ResetToDefaults";
    public const string Opt_ResetToDefaultsDone = "SubCraftica.Opt_ResetToDefaultsDone";
    public const string StorageMode_Disabled = "SubCraftica.StorageMode_Disabled";
    public const string StorageMode_Nearby = "SubCraftica.StorageMode_Nearby";
    public const string StorageMode_AllLoaded = "SubCraftica.StorageMode_AllLoaded";
    public const string CraftingMode_PerItem = "SubCraftica.CraftingMode_PerItem";
    public const string CraftingMode_Batch = "SubCraftica.CraftingMode_Batch";
    public const string CraftingMode_Instant = "SubCraftica.CraftingMode_Instant";
    public const string CraftingTooltipMode_Disabled = "SubCraftica.CraftingTooltipMode_Disabled";
    public const string CraftingTooltipMode_Basic = "SubCraftica.CraftingTooltipMode_Basic";
    public const string CraftingTooltipMode_Advanced = "SubCraftica.CraftingTooltipMode_Advanced";
    public const string Tooltip_AdjustAmount = "SubCraftica.Tooltip_AdjustAmount";
    public const string Tooltip_Amount = "SubCraftica.Tooltip_Amount";
    public const string Tooltip_Total = "SubCraftica.Tooltip_Total";
    public const string OptDesc_InternalExtractionExcludedStorageIds = "SubCraftica.OptDesc_InternalExtractionExcludedStorageIds";
    public const string OptDesc_InternalPreferredSurplusStorageIds = "SubCraftica.OptDesc_InternalPreferredSurplusStorageIds";
    public const string Tooltip_Recycle = "SubCraftica.Tooltip_Recycle";

    private static readonly object SyncRoot = new object();
    private static readonly FieldInfo CurrentLanguageField = typeof(Language).GetField("currentLanguage", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static Dictionary<string, string> translations;
    private static string loadedLanguage = string.Empty;

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        EnsureLoaded();
        return translations != null && translations.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, Get(key), args);
    }

    private static void EnsureLoaded()
    {
        var gameLanguage = ResolveGameLanguage();
        if (translations != null && string.Equals(loadedLanguage, gameLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        lock (SyncRoot)
        {
            gameLanguage = ResolveGameLanguage();
            if (translations != null && string.Equals(loadedLanguage, gameLanguage, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            loadedLanguage = gameLanguage;
            translations = LoadTranslations(gameLanguage);
        }
    }

    private static Dictionary<string, string> LoadTranslations(string gameLanguage)
    {
        var baseDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (string.IsNullOrWhiteSpace(baseDirectory))
        {
            return CreateEmptyTranslations();
        }

        var languageFolder = Path.Combine(baseDirectory, "lang");
        foreach (var candidate in GetCandidatePaths(languageFolder, gameLanguage))
        {
            var loaded = TryLoadFile(candidate);
            if (loaded != null)
            {
                return loaded;
            }
        }

        return CreateEmptyTranslations();
    }

    private static IEnumerable<string> GetCandidatePaths(string languageFolder, string gameLanguage)
    {
        var normalized = NormalizeLanguageName(gameLanguage);

        if (!string.IsNullOrWhiteSpace(normalized))
        {
            yield return Path.Combine(languageFolder, normalized + ".json");
        }

        if (string.Equals(normalized, "SpanishLatinAmerica", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(languageFolder, "Spanish.json");
        }
        else if (string.Equals(normalized, "Spanish", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(languageFolder, "SpanishLatinAmerica.json");
        }

        yield return Path.Combine(languageFolder, "English.json");
    }

    private static string NormalizeLanguageName(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "English";
        }

        var normalized = language.Trim();
        var compact = normalized.Replace(" ", string.Empty).Replace("-", string.Empty).Replace("_", string.Empty);

        switch (compact.ToLowerInvariant())
        {
            case "english": return "English";
            case "spanish": return "Spanish";
            case "spanishlatinamerica":
            case "es419":
            case "latamspanish":
                return "SpanishLatinAmerica";
            case "french": return "French";
            case "german": return "German";
            case "polish": return "Polish";
            case "russian": return "Russian";
            case "chinesesimplified":
            case "schinese":
            case "zhhans":
                return "ChineseSimplified";
            case "turkish": return "Turkish";
            case "finnish": return "Finnish";
            case "italian": return "Italian";
            case "czech": return "Czech";
            case "hungarian": return "Hungarian";
            case "danish": return "Danish";
            case "japanese": return "Japanese";
            case "korean": return "Korean";
            case "portuguesebrazil":
            case "brazilianportuguese":
            case "ptbr":
                return "PortugueseBrazil";
            case "bulgarian": return "Bulgarian";
            case "ukrainian": return "Ukrainian";
            case "dutch": return "Dutch";
            case "swedish": return "Swedish";
            case "vietnamese": return "Vietnamese";
            case "chinesetraditional":
            case "tchinese":
            case "zhhant":
                return "ChineseTraditional";
            case "portugueseportugal":
            case "europeanportuguese":
            case "ptpt":
                return "PortuguesePortugal";
            case "latvian": return "Latvian";
            case "lithuanian": return "Lithuanian";
            case "slovak": return "Slovak";
            default:
                return normalized;
        }
    }

    private static string ResolveGameLanguage()
    {
        var liveLanguage = TryGetLiveGameLanguage();
        if (!string.IsNullOrWhiteSpace(liveLanguage))
        {
            return liveLanguage;
        }

        var savedLanguage = TryGetSavedGameLanguage();
        if (!string.IsNullOrWhiteSpace(savedLanguage))
        {
            return savedLanguage;
        }

        return "English";
    }

    private static string TryGetLiveGameLanguage()
    {
        if (Language.main == null)
        {
            return null;
        }

        return CurrentLanguageField?.GetValue(Language.main)?.ToString();
    }

    private static string TryGetSavedGameLanguage()
    {
        foreach (var key in new[] { "Language", "language" })
        {
            try
            {
                if (!PlayerPrefs.HasKey(key))
                {
                    continue;
                }

                var value = PlayerPrefs.GetString(key, string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            catch
            {
                return null;
            }
        }

        return null;
    }

    private static Dictionary<string, string> TryLoadFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)
                ?? CreateEmptyTranslations();
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> CreateEmptyTranslations()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    }
}
