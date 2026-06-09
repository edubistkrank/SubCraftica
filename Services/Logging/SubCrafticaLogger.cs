using BepInEx.Logging;

namespace SubCraftica.Services.Logging;

internal static class SubCrafticaLogger
{
    internal static void Initialize()
    {
    }

    internal static void LogInfo(string message)
    {
        Log(LogLevel.Info, message);
    }

    internal static void LogWarning(string message)
    {
        Log(LogLevel.Warning, message);
    }

    internal static void LogError(string message)
    {
        Log(LogLevel.Error, message);
    }

    internal static void LogDebug(string message)
    {
        // Debug traces disabled in release workflow.
    }

    private static void Log(LogLevel level, string message)
    {
        try
        {
            Plugin.Log?.Log(level, message);
        }
        catch { }
    }

    internal static void Dispose()
    {
    }
}
