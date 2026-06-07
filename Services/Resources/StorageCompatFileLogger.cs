using System;
using System.IO;
using System.Text;

namespace SubCraftica.Services.Resources;

internal static class StorageCompatFileLogger
{
    private static readonly object sync = new object();
    private static readonly string logDir;
    private static readonly string logFile;

    static StorageCompatFileLogger()
    {
        try
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory ?? ".";
            logDir = Path.Combine(baseDir, "BepInEx", "plugins", "SubCraftica");
            Directory.CreateDirectory(logDir);
            logFile = Path.Combine(logDir, "compat-debug.log");
        }
        catch
        {
            logDir = null;
            logFile = null;
        }
    }

    public static void WriteLine(string message)
    {
        if (string.IsNullOrEmpty(logFile))
            return;

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
        try
        {
            lock (sync)
            {
                File.AppendAllText(logFile, line, Encoding.UTF8);
            }
        }
        catch
        {
            // best-effort logging only
        }
    }

    public static void LogInfo(string message) => WriteLine("INFO: " + message);
    public static void LogWarning(string message) => WriteLine("WARN: " + message);
    public static void LogError(string message) => WriteLine("ERR: " + message);
}
