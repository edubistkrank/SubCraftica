using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace SubCraftica.Services.Compat;

internal sealed class PowerSaverCompatService
{
    private const string PowerSaverGuid = "com.zerotheabsolute.powersaver";
    private const string PowerSaverPluginTypeName = "PowerSaver.PowerSaverPlugin, PowerSaver";

    private bool resolved;
    private float baseDrainMultiplier = 1f;

    public float GetBaseDrainMultiplier()
    {
        if (!resolved)
        {
            Resolve();
        }

        return baseDrainMultiplier;
    }

    private void Resolve()
    {
        resolved = true;
        baseDrainMultiplier = 1f;

        try
        {
            if (Chainloader.PluginInfos == null || !Chainloader.PluginInfos.ContainsKey(PowerSaverGuid))
            {
                return;
            }

            var pluginType = Type.GetType(PowerSaverPluginTypeName, throwOnError: false);
            if (pluginType == null)
            {
                return;
            }

            var globalDrain = ReadConfigEntryFloat(pluginType, "DrainMultiplier", 1f);
            var baseDrain = ReadConfigEntryFloat(pluginType, "BaseDrainMultiplier", 1f);

            var combined = globalDrain * baseDrain;
            if (combined > 0f)
            {
                baseDrainMultiplier = combined;
            }
        }
        catch
        {
            baseDrainMultiplier = 1f;
        }
    }

    private static float ReadConfigEntryFloat(Type pluginType, string fieldName, float fallback)
    {
        var field = pluginType.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null)
        {
            return fallback;
        }

        var entry = field.GetValue(null);
        if (entry == null)
        {
            return fallback;
        }

        var valueProperty = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        if (valueProperty == null)
        {
            return fallback;
        }

        var raw = valueProperty.GetValue(entry, null);
        if (raw is float typed && typed > 0f)
        {
            return typed;
        }

        try
        {
            var converted = Convert.ToSingle(raw);
            return converted > 0f ? converted : fallback;
        }
        catch
        {
            return fallback;
        }
    }
}
