using System;
using System.IO;
using BepInEx.Logging;

namespace SubCraftica.Services.Logging;

/// <summary>
/// Dedicated logger for SubCraftica with file output for debugging.
/// Creates log file in the same directory as the plugin DLL.
/// </summary>
internal static class SubCrafticaLogger
{
    private static StreamWriter logFileWriter;
    private static readonly object logLock = new object();
    private static string pluginDirectory;

    private static string LogFilePath
    {
        get
        {
            if (pluginDirectory == null)
            {
                // Get the directory of the SubCraftica plugin
                var assemblyLocation = typeof(Plugin).Assembly.Location;
                pluginDirectory = Path.GetDirectoryName(assemblyLocation) ?? ".";
            }

            return Path.Combine(pluginDirectory, "SubCraftica_Debug.log");
        }
    }

    internal static void Initialize()
    {
        try
        {
            lock (logLock)
            {
                var logPath = LogFilePath;
                var logDir = Path.GetDirectoryName(logPath);

                if (!Directory.Exists(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                logFileWriter = new StreamWriter(logPath, append: true) { AutoFlush = true };
                LogInfo("=== SubCraftica Debug Log Initialized ===");
                LogInfo($"Log file location: {logPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"Could not initialize SubCraftica debug logger: {ex.Message}");
        }
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
        Log(LogLevel.Debug, message);
    }

    private static void Log(LogLevel level, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var prefix = $"[{timestamp}] [{level}]";
        var fullMessage = $"{prefix} {message}";

        try
        {
            Plugin.Log?.Log(level, fullMessage);
        }
        catch { }

        try
        {
            lock (logLock)
            {
                if (logFileWriter != null)
                {
                    logFileWriter.WriteLine(fullMessage);
                }
            }
        }
        catch { }
    }

    internal static void Dispose()
    {
        lock (logLock)
        {
            logFileWriter?.Dispose();
            logFileWriter = null;
        }
    }
}
