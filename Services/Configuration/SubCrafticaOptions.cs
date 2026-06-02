using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Nautilus.Options;
using SubCraftica.Services.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace SubCraftica.Services.Configuration;

internal sealed class SubCrafticaOptions : ModOptions
{
    private const string SectionCraftingId = "SubCraftica.Section.Crafting";
    private const string SectionStorageId = "SubCraftica.Section.Storage";
    private const string SectionEnergyId = "SubCraftica.Section.Energy";
    private const string SectionColorsId = "SubCraftica.Section.Colors";

    private const string OptionEnableAutoSubcraftId = nameof(ModText.Opt_EnableAutoSubcraft);
    private const string OptionCreativeModeId = nameof(ModText.Opt_CreativeMode);
    private const string OptionCraftingTooltipModeId = nameof(ModText.Opt_CraftingTooltipMode);
    private const string OptionResetToDefaultsId = nameof(ModText.Opt_ResetToDefaults);
    private const string OptionCraftingModeId = nameof(ModText.Opt_CraftingMode);
    private const string OptionMaxUnitsPerRequestId = nameof(ModText.Opt_MaxUnitsPerRequest);
    private const string OptionStorageModeId = nameof(ModText.Opt_StorageMode);
    private const string OptionStorageRangeId = nameof(ModText.Opt_StorageRange);
    private const string OptionReturnSurplusToStorageId = nameof(ModText.Opt_ReturnSurplusToStorage);
    private const string OptionBlacklistToggleId = nameof(ModText.Opt_BlacklistToggle);
    private const string OptionSurplusToggleId = nameof(ModText.Opt_SurplusToggle);
    private const string OptionEnergyMultiplierId = nameof(ModText.Opt_EnergyMultiplier);
    private const string OptionIncludeSubrecipeEnergyId = nameof(ModText.Opt_IncludeSubrecipeEnergy);
    private const string OptionStorageOnlyIngredientColorId = nameof(ModText.Opt_StorageOnlyIngredientColor);
    private const string OptionInventoryIngredientColorId = nameof(ModText.Opt_InventoryIngredientColor);
    private const string OptionMissingIngredientColorId = nameof(ModText.Opt_MissingIngredientColor);

    private readonly ModConfig config;
    private readonly Dictionary<string, GameObject> optionGameObjects = new Dictionary<string, GameObject>(StringComparer.Ordinal);

    public SubCrafticaOptions(ModConfig config) : base("SubCraftica")
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        this.config = config;
        GameObjectCreated += OnGameObjectCreated;

        AddGeneralOptions();
        AddCraftingOptions();
        AddStorageOptions();
        AddEnergyOptions();
        AddColorOptions();
    }

    private void AddGeneralOptions()
    {
        AddToggle(
            config.EnableAutoSubcraft,
            OptionEnableAutoSubcraftId,
            ModText.Get(ModText.Opt_EnableAutoSubcraft),
            ModText.Get(ModText.OptDesc_EnableAutoSubcraft));

        AddToggle(
            config.CreativeMode,
            OptionCreativeModeId,
            ModText.Get(ModText.Opt_CreativeMode),
            ModText.Get(ModText.OptDesc_CreativeMode));

        AddCraftingTooltipModeChoice(
            config.CraftingTooltipMode,
            OptionCraftingTooltipModeId,
            ModText.Get(ModText.Opt_CraftingTooltipMode),
            ModText.Get(ModText.OptDesc_CraftingTooltipMode));

        AddResetButton();
    }

    private void AddCraftingOptions()
    {
        AddSectionLabel(SectionCraftingId, ModText.Get(ModText.OptGroup_Crafting));

        AddCraftingModeChoice(
            config.CraftingMode,
            OptionCraftingModeId,
            ModText.Get(ModText.Opt_CraftingMode),
            ModText.Get(ModText.OptDesc_CraftingMode));

        AddSlider(
            config.MaxQueueSize,
            OptionMaxUnitsPerRequestId,
            ModText.Get(ModText.Opt_MaxUnitsPerRequest),
            1,
            200,
            1,
            ModText.Get(ModText.OptDesc_MaxUnitsPerRequest));
    }

    private void AddStorageOptions()
    {
        AddSectionLabel(SectionStorageId, ModText.Get(ModText.OptGroup_Storage));

        AddStorageModeChoice(
            config.StorageCraftMode,
            OptionStorageModeId,
            ModText.Get(ModText.Opt_StorageMode),
            ModText.Get(ModText.OptDesc_StorageMode));

        AddSlider(
            config.StorageRange,
            OptionStorageRangeId,
            ModText.Get(ModText.Opt_StorageRange),
            5f,
            500f,
            5f,
            ModText.Get(ModText.OptDesc_StorageRange));

        AddToggle(
            config.ReturnSurplusToStorage,
            OptionReturnSurplusToStorageId,
            ModText.Get(ModText.Opt_ReturnSurplusToStorage),
            ModText.Get(ModText.OptDesc_ReturnSurplusToStorage));

        AddToggle(
            config.EnableBlacklistToggle,
            OptionBlacklistToggleId,
            ModText.Get(ModText.Opt_BlacklistToggle),
            ModText.Get(ModText.OptDesc_BlacklistToggle));

        AddToggle(
            config.EnableSurplusToggle,
            OptionSurplusToggleId,
            ModText.Get(ModText.Opt_SurplusToggle),
            ModText.Get(ModText.OptDesc_SurplusToggle));
    }

    private void AddEnergyOptions()
    {
        AddSectionLabel(SectionEnergyId, ModText.Get(ModText.OptGroup_Energy));

        AddEnergySlider(
            config.CraftEnergyMultiplier,
            OptionEnergyMultiplierId,
            ModText.Get(ModText.Opt_EnergyMultiplier),
            0f,
            10f,
            0.5f,
            ModText.Get(ModText.OptDesc_EnergyMultiplier));

        AddToggle(
            config.IncludeSubrecipeEnergyCost,
            OptionIncludeSubrecipeEnergyId,
            ModText.Get(ModText.Opt_IncludeSubrecipeEnergy),
            ModText.Get(ModText.OptDesc_IncludeSubrecipeEnergy));
    }

    private void AddColorOptions()
    {
        AddSectionLabel(SectionColorsId, ModText.Get(ModText.OptGroup_Colors));

        AddStorageIngredientColorChoice(
            config.StorageOnlyIngredientColorPreset,
            OptionStorageOnlyIngredientColorId,
            ModText.Get(ModText.Opt_StorageOnlyIngredientColor),
            ModText.Get(ModText.OptDesc_StorageOnlyIngredientColor));

        AddInventoryIngredientColorChoice(
            config.InventoryIngredientColorPreset,
            OptionInventoryIngredientColorId,
            ModText.Get(ModText.Opt_InventoryIngredientColor),
            ModText.Get(ModText.OptDesc_InventoryIngredientColor));

        AddMissingIngredientColorChoice(
            config.MissingIngredientColorPreset,
            OptionMissingIngredientColorId,
            ModText.Get(ModText.Opt_MissingIngredientColor),
            ModText.Get(ModText.OptDesc_MissingIngredientColor));
    }

    private void OnGameObjectCreated(object sender, GameObjectCreatedEventArgs args)
    {
        if (args == null || args.Value == null || string.IsNullOrWhiteSpace(args.Id))
        {
            return;
        }

        optionGameObjects[args.Id] = args.Value;
    }

    private void AddSectionLabel(string id, string title)
    {
        var option = ModToggleOption.Create(id, BuildSectionLabel(title), false);
        option.OnChanged += (_, __) => { };
        AddItem(option);
    }

    private static string BuildSectionLabel(string title)
    {
        var safeTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
        return $"<color=#FFAC09FF>{safeTitle}</color> <alpha=#00>----------------------------------------------------------------------------</alpha>";
    }

    private OptionItem AddResetButton()
    {
        var label = ModText.Get(ModText.Opt_ResetToDefaults);
        var tooltip = ModText.Get(ModText.OptDesc_ResetToDefaults);
        var option = ModButtonOption.Create(OptionResetToDefaultsId, label, _ => ResetToDefaults(), tooltip);
        AddItem(option);
        return option;
    }

    private void ResetToDefaults()
    {
        ApplyDefaultValues();
        config.EnableAutoSubcraft.ConfigFile.Save();
        RefreshControlsFromConfig();
        ErrorMessage.AddMessage(ModText.Get(ModText.Opt_ResetToDefaultsDone));
    }

    private void ApplyDefaultValues()
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        config.EnableAutoSubcraft.Value = true;
        config.CreativeMode.Value = false;
        config.CraftingTooltipMode.Value = ModConfig.CraftingTooltipModeAdvanced;
        config.StorageCraftMode.Value = ModConfig.StorageModeNearby;
        config.StorageRange.Value = 100f;
        config.StorageOnlyIngredientColorPreset.Value = ModConfig.StorageColorPresetMin;
        config.InventoryIngredientColorPreset.Value = ModConfig.InventoryColorPresetMin;
        config.MissingIngredientColorPreset.Value = ModConfig.MissingColorPresetMin;
        config.MaxQueueSize.Value = 50;
        config.CraftingMode.Value = ModConfig.CraftingModePerItem;
        config.CraftEnergyMultiplier.Value = 1f;
        config.IncludeSubrecipeEnergyCost.Value = true;
        config.ReturnSurplusToStorage.Value = true;
        config.EnableBlacklistToggle.Value = true;
        config.EnableSurplusToggle.Value = true;
        config.ExtractionExcludedStorageIds.Value = string.Empty;
        config.PreferredSurplusStorageIds.Value = string.Empty;
    }

    private void RefreshControlsFromConfig()
    {
        SetToggleValue(OptionEnableAutoSubcraftId, config.EnableAutoSubcraft.Value);
        SetToggleValue(OptionCreativeModeId, config.CreativeMode.Value);
        SetChoiceValue(OptionCraftingTooltipModeId, config.CraftingTooltipMode.Value);
        SetChoiceValue(OptionCraftingModeId, config.CraftingMode.Value);
        SetSliderValue(OptionMaxUnitsPerRequestId, config.MaxQueueSize.Value);
        SetChoiceValue(OptionStorageModeId, config.StorageCraftMode.Value);
        SetSliderValue(OptionStorageRangeId, config.StorageRange.Value);
        SetToggleValue(OptionReturnSurplusToStorageId, config.ReturnSurplusToStorage.Value);
        SetToggleValue(OptionBlacklistToggleId, config.EnableBlacklistToggle.Value);
        SetToggleValue(OptionSurplusToggleId, config.EnableSurplusToggle.Value);
        SetSliderValue(OptionEnergyMultiplierId, config.CraftEnergyMultiplier.Value);
        SetToggleValue(OptionIncludeSubrecipeEnergyId, config.IncludeSubrecipeEnergyCost.Value);
        SetChoiceValue(OptionStorageOnlyIngredientColorId, config.StorageOnlyIngredientColorPreset.Value);
        SetChoiceValue(OptionInventoryIngredientColorId, config.InventoryIngredientColorPreset.Value);
        SetChoiceValue(OptionMissingIngredientColorId, config.MissingIngredientColorPreset.Value);
    }

    private void SetToggleValue(string optionId, bool value)
    {
        if (!TryGetOptionGameObject(optionId, out var gameObject))
        {
            return;
        }

        var toggle = gameObject.GetComponentInChildren<Toggle>(true);
        if (toggle != null)
        {
            toggle.isOn = value;
        }
    }

    private void SetSliderValue(string optionId, float value)
    {
        if (!TryGetOptionGameObject(optionId, out var gameObject))
        {
            return;
        }

        var slider = gameObject.GetComponentInChildren<uGUI_SnappingSlider>(true);
        if (slider != null)
        {
            slider.value = value;
            return;
        }

        var unitySlider = gameObject.GetComponentInChildren<Slider>(true);
        if (unitySlider != null)
        {
            unitySlider.value = value;
        }
    }

    private void SetChoiceValue(string optionId, int value)
    {
        if (!TryGetOptionGameObject(optionId, out var gameObject))
        {
            return;
        }

        var choice = gameObject.GetComponentInChildren<uGUI_Choice>(true);
        if (choice != null)
        {
            choice.value = value;
        }
    }

    private bool TryGetOptionGameObject(string optionId, out GameObject gameObject)
    {
        return optionGameObjects.TryGetValue(optionId, out gameObject) && gameObject != null;
    }

    private OptionItem AddStorageModeChoice(ConfigEntry<int> entry, string id, string label, string tooltip)
    {
        return AddClampedChoice(entry, id, label, tooltip, new[]
        {
            ModText.Get(ModText.StorageMode_Disabled),
            ModText.Get(ModText.StorageMode_Nearby),
            ModText.Get(ModText.StorageMode_AllLoaded)
        }, ModConfig.StorageModeDisabled, ModConfig.StorageModeAllLoaded);
    }

    private OptionItem AddCraftingTooltipModeChoice(ConfigEntry<int> entry, string id, string label, string tooltip)
    {
        return AddClampedChoice(entry, id, label, tooltip, new[]
        {
            ModText.Get(ModText.CraftingTooltipMode_Disabled),
            ModText.Get(ModText.CraftingTooltipMode_Basic),
            ModText.Get(ModText.CraftingTooltipMode_Advanced)
        }, ModConfig.CraftingTooltipModeDisabled, ModConfig.CraftingTooltipModeAdvanced);
    }

    private OptionItem AddToggle(ConfigEntry<bool> entry, string id, string label, string tooltip)
    {
        var option = ModToggleOption.Create(id, label, entry.Value, tooltip);
        option.OnChanged += (_, args) => entry.Value = args.Value;
        AddItem(option);
        return option;
    }

    private OptionItem AddSlider(ConfigEntry<float> entry, string id, string label, float min, float max, float step, string tooltip)
    {
        var defaultValue = entry.DefaultValue is null
            ? (float?)null
            : Convert.ToSingle(entry.DefaultValue);
        var option = ModSliderOption.Create(id, label, min, max, entry.Value, defaultValue, "{0:F0}", step, tooltip);
        option.OnChanged += (_, args) => entry.Value = ClampAndSnap(args.Value, min, max, step);
        AddItem(option);
        return option;
    }

    private OptionItem AddCraftingModeChoice(ConfigEntry<int> entry, string id, string label, string tooltip)
    {
        return AddClampedChoice(entry, id, label, tooltip, new[]
        {
            ModText.Get(ModText.CraftingMode_PerItem),
            ModText.Get(ModText.CraftingMode_Batch),
            ModText.Get(ModText.CraftingMode_Instant)
        }, ModConfig.CraftingModePerItem, ModConfig.CraftingModeInstant);
    }

    private OptionItem AddEnergySlider(ConfigEntry<float> entry, string id, string label, float min, float max, float step, string tooltip)
    {
        var defaultValue = entry.DefaultValue is null
            ? (float?)null
            : Convert.ToSingle(entry.DefaultValue);
        var option = ModSliderOption.Create(id, label, min, max, entry.Value, defaultValue, "{0:0.0#}", step, tooltip);
        option.OnChanged += (_, args) => entry.Value = ClampAndSnap(args.Value, min, max, step);
        AddItem(option);
        return option;
    }

    private OptionItem AddSlider(ConfigEntry<int> entry, string id, string label, int min, int max, int step, string tooltip)
    {
        var defaultValue = entry.DefaultValue is null
            ? (float?)null
            : Convert.ToSingle(entry.DefaultValue);
        var option = ModSliderOption.Create(id, label, min, max, entry.Value, defaultValue, "{0:F0}", step, tooltip);
        option.OnChanged += (_, args) => entry.Value = (int)args.Value;
        AddItem(option);
        return option;
    }

    private OptionItem AddStorageIngredientColorChoice(ConfigEntry<int> entry, string id, string label, string tooltip)
    {
        return AddClampedChoice(entry, id, label, tooltip, StorageIngredientColorPresets.ChoiceLabels, ModConfig.StorageColorPresetMin, StorageIngredientColorPresets.ChoiceLabels.Length - 1);
    }

    private OptionItem AddInventoryIngredientColorChoice(ConfigEntry<int> entry, string id, string label, string tooltip)
    {
        return AddClampedChoice(entry, id, label, tooltip, StorageIngredientColorPresets.ChoiceLabels, ModConfig.InventoryColorPresetMin, StorageIngredientColorPresets.ChoiceLabels.Length - 1);
    }

    private OptionItem AddMissingIngredientColorChoice(ConfigEntry<int> entry, string id, string label, string tooltip)
    {
        return AddClampedChoice(entry, id, label, tooltip, MissingIngredientColorPresets.ChoiceLabels, ModConfig.MissingColorPresetMin, MissingIngredientColorPresets.ChoiceLabels.Length - 1);
    }

    private OptionItem AddClampedChoice(ConfigEntry<int> entry, string id, string label, string tooltip, string[] choices, int min, int max)
    {
        var selectedIndex = Math.Max(min, Math.Min(max, entry.Value));
        entry.Value = selectedIndex;

        var option = ModChoiceOption<string>.Create(id, label, choices, selectedIndex, tooltip);
        option.OnChanged += (_, args) => entry.Value = Math.Max(min, Math.Min(max, args.Index));

        AddItem(option);
        return option;
    }

    private static float ClampAndSnap(float value, float min, float max, float step)
    {
        var clamped = Math.Max(min, Math.Min(max, value));
        return (float)Math.Round(clamped / step, MidpointRounding.AwayFromZero) * step;
    }
}
