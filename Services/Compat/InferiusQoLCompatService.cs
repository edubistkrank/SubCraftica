using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace SubCraftica.Services.Compat;

internal sealed class InferiusQoLCompatService
{
    private const string InferiusGuid = "InferiusQoL";
    private const string ConfigTypeName = "InferiusQoL.Config.InferiusConfig, InferiusQoL";

    private Type configType;
    private PropertyInfo instanceProperty;
    private FieldInfo autoCraftEnabledField;
    private FieldInfo autoCraftBetterTooltipsField;

    private bool initialized;
    private bool warned;
    private bool disabledAutoCraft;

    public bool IsInstalled => Chainloader.PluginInfos != null && Chainloader.PluginInfos.ContainsKey(InferiusGuid);

    public void Tick()
    {
        if (!IsInstalled)
        {
            return;
        }

        EnsureInitialized();
        if (!initialized)
        {
            return;
        }

        try
        {
            var instance = instanceProperty?.GetValue(null, null);
            if (instance == null)
            {
                return;
            }

            var changed = false;
            if (autoCraftEnabledField != null)
            {
                var autoCraftEnabled = autoCraftEnabledField.GetValue(instance);
                if (autoCraftEnabled is bool enabled && enabled)
                {
                    autoCraftEnabledField.SetValue(instance, false);
                    changed = true;
                }
            }

            if (autoCraftBetterTooltipsField != null)
            {
                var betterTooltips = autoCraftBetterTooltipsField.GetValue(instance);
                if (betterTooltips is bool enabled && enabled)
                {
                    autoCraftBetterTooltipsField.SetValue(instance, false);
                    changed = true;
                }
            }

            if (changed)
            {
                disabledAutoCraft = true;
            }

            if (disabledAutoCraft && !warned)
            {
                warned = true;
                ErrorMessage.AddMessage("SubCraftica: InferiusQoL AutoCraft disabled for compatibility.");
                Plugin.Log?.LogInfo("InferiusQoL compatibility: AutoCraft and BetterTooltips were disabled to avoid conflicts.");
            }
        }
        catch (Exception ex)
        {
            if (!warned)
            {
                warned = true;
                Plugin.Log?.LogWarning($"InferiusQoL compatibility could not apply: {ex.Message}");
            }
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        initialized = true;

        configType = Type.GetType(ConfigTypeName, throwOnError: false);
        if (configType == null)
        {
            return;
        }

        instanceProperty = configType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        autoCraftEnabledField = configType.GetField("AutoCraftEnabled", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        autoCraftBetterTooltipsField = configType.GetField("AutoCraftBetterTooltips", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    }
}
