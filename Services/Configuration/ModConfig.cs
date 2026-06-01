using System;
using BepInEx.Configuration;
using SubCraftica.Services.Localization;

namespace SubCraftica.Services.Configuration;

internal sealed class ModConfig
{
    public const int CraftingModePerItem = 0;
    public const int CraftingModeBatch = 1;
    public const int CraftingModeInstant = 2;

    public const int StorageModeDisabled = 0;
    public const int StorageModeNearby = 1;
    public const int StorageModeAllLoaded = 2;
    public const int StorageColorPresetMin = 0;
    public const int StorageColorPresetMax = 16;
    public const int InventoryColorPresetMin = 0;
    public const int InventoryColorPresetMax = 16;
    public const int MissingColorPresetMin = 0;
    public const int MissingColorPresetMax = 9;
    public const int CraftingTooltipModeDisabled = 0;
    public const int CraftingTooltipModeBasic = 1;
    public const int CraftingTooltipModeAdvanced = 2;

    public ConfigEntry<bool> EnableAutoSubcraft { get; private set; }
    public ConfigEntry<int> CraftingTooltipMode { get; private set; }
    public ConfigEntry<int> StorageCraftMode { get; private set; }
    public ConfigEntry<float> StorageRange { get; private set; }
    public ConfigEntry<int> StorageOnlyIngredientColorPreset { get; private set; }
    public ConfigEntry<int> InventoryIngredientColorPreset { get; private set; }
    public ConfigEntry<int> MissingIngredientColorPreset { get; private set; }
    public ConfigEntry<int> MaxQueueSize { get; private set; }
    public ConfigEntry<int> CraftingMode { get; private set; }
    public ConfigEntry<float> CraftEnergyMultiplier { get; private set; }
    public ConfigEntry<bool> IncludeSubrecipeEnergyCost { get; private set; }
    public ConfigEntry<bool> ReturnSurplusToStorage { get; private set; }
    public ConfigEntry<bool> EnableBlacklistToggle { get; private set; }
    public ConfigEntry<bool> EnableSurplusToggle { get; private set; }
    public ConfigEntry<string> ExtractionExcludedStorageIds { get; private set; }
    public ConfigEntry<string> PreferredSurplusStorageIds { get; private set; }

    public static ModConfig Bind(ConfigFile config)
    {
        var result = new ModConfig
        {
            EnableAutoSubcraft = config.Bind("General", "EnableAutoSubcraft", true, ModText.Get(ModText.OptDesc_EnableAutoSubcraft)),
            CraftingTooltipMode = config.Bind("General", "CraftingTooltipMode", CraftingTooltipModeAdvanced, ModText.Get(ModText.OptDesc_CraftingTooltipMode)),
            StorageCraftMode = config.Bind("General", "StorageCraftMode", StorageModeNearby, ModText.Get(ModText.OptDesc_StorageMode)),
            StorageRange = config.Bind("General", "StorageRange", 100f, ModText.Get(ModText.OptDesc_StorageRange)),
            StorageOnlyIngredientColorPreset = config.Bind("General", "StorageOnlyIngredientColorPreset", StorageColorPresetMin, ModText.Get(ModText.OptDesc_StorageOnlyIngredientColor)),
            InventoryIngredientColorPreset = config.Bind("General", "InventoryIngredientColorPreset", InventoryColorPresetMin, ModText.Get(ModText.OptDesc_InventoryIngredientColor)),
            MissingIngredientColorPreset = config.Bind("General", "MissingIngredientColorPreset", MissingColorPresetMin, ModText.Get(ModText.OptDesc_MissingIngredientColor)),
            MaxQueueSize = config.Bind("Queue", "MaxQueueSize", 50, ModText.Get(ModText.OptDesc_MaxUnitsPerRequest)),
            CraftingMode = config.Bind("Crafting", "CraftingMode", CraftingModePerItem, ModText.Get(ModText.OptDesc_CraftingMode)),
            CraftEnergyMultiplier = config.Bind("Crafting", "CraftEnergyMultiplier", 1f, ModText.Get(ModText.OptDesc_EnergyMultiplier)),
            IncludeSubrecipeEnergyCost = config.Bind("Crafting", "IncludeSubrecipeEnergyCost", true, ModText.Get(ModText.OptDesc_IncludeSubrecipeEnergy)),
            ReturnSurplusToStorage = config.Bind("General", "ReturnSurplusToStorage", true, ModText.Get(ModText.OptDesc_ReturnSurplusToStorage)),
            EnableBlacklistToggle = config.Bind("General", "EnableBlacklistToggle", true, ModText.Get(ModText.OptDesc_BlacklistToggle)),
            EnableSurplusToggle = config.Bind("General", "EnableSurplusToggle", true, ModText.Get(ModText.OptDesc_SurplusToggle)),
            ExtractionExcludedStorageIds = config.Bind("Internal", "ExtractionExcludedStorageIds", string.Empty, ModText.Get(ModText.OptDesc_InternalExtractionExcludedStorageIds)),
            PreferredSurplusStorageIds = config.Bind("Internal", "PreferredSurplusStorageIds", string.Empty, ModText.Get(ModText.OptDesc_InternalPreferredSurplusStorageIds))
        };

        if (result.CraftingMode.Value < CraftingModePerItem || result.CraftingMode.Value > CraftingModeInstant)
        {
            result.CraftingMode.Value = CraftingModePerItem;
        }

        if (result.CraftingTooltipMode.Value < CraftingTooltipModeDisabled || result.CraftingTooltipMode.Value > CraftingTooltipModeAdvanced)
        {
            result.CraftingTooltipMode.Value = CraftingTooltipModeAdvanced;
        }

        if (result.StorageCraftMode.Value < StorageModeDisabled || result.StorageCraftMode.Value > StorageModeAllLoaded)
        {
            result.StorageCraftMode.Value = StorageModeNearby;
        }

        if (result.StorageOnlyIngredientColorPreset.Value < StorageColorPresetMin || result.StorageOnlyIngredientColorPreset.Value > StorageColorPresetMax)
        {
            result.StorageOnlyIngredientColorPreset.Value = StorageColorPresetMin;
        }

        if (result.InventoryIngredientColorPreset.Value < InventoryColorPresetMin || result.InventoryIngredientColorPreset.Value > InventoryColorPresetMax)
        {
            result.InventoryIngredientColorPreset.Value = InventoryColorPresetMin;
        }

        if (result.MissingIngredientColorPreset.Value < MissingColorPresetMin || result.MissingIngredientColorPreset.Value > MissingColorPresetMax)
        {
            result.MissingIngredientColorPreset.Value = MissingColorPresetMin;
        }

        var snappedRange = (float)Math.Round(Math.Max(5f, Math.Min(500f, result.StorageRange.Value)) / 5f, MidpointRounding.AwayFromZero) * 5f;
        result.StorageRange.Value = snappedRange;

        return result;
    }
}