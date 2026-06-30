using BepInEx.Logging;
using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace SubCraftica.Services.Logging;

internal static class SubCrafticaLogger
{
    private static readonly object Sync = new object();
    private static string logFilePath;

    internal static void Initialize()
    {
        try
        {
            var assemblyPath = Assembly.GetExecutingAssembly().Location;
            var baseDirectory = Path.GetDirectoryName(assemblyPath);
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return;
            }

            logFilePath = Path.Combine(baseDirectory, "SubCraftica.log");
            AppendToFile(LogLevel.Info, "=== SubCraftica logging session started ===");
        }
        catch
        {
            logFilePath = null;
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
        // Debug traces disabled in release workflow.
    }

    private static void Log(LogLevel level, string message)
    {
        try
        {
            Plugin.Log?.Log(level, message);
            AppendToFile(level, message);
        }
        catch { }
    }

    private static void AppendToFile(LogLevel level, string message)
    {
        if (string.IsNullOrWhiteSpace(logFilePath))
        {
            return;
        }

        try
        {
            var line = string.Concat(
                "[",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                "] [",
                level.ToString(),
                "] ",
                message,
                Environment.NewLine);

            lock (Sync)
            {
                File.AppendAllText(logFilePath, line, Encoding.UTF8);
            }
        }
        catch
        {
        }
    }

    internal static void Dispose()
    {
        AppendToFile(LogLevel.Info, "=== SubCraftica logging session ended ===");
    }
}
