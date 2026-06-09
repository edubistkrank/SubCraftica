using System;
using System.IO;
using BepInEx.Logging;

namespace SubCraftica.Services.Logging;

internal static class PrototypeCompatDebugLogger
{
    private static readonly object Sync = new object();
    private static StreamWriter _writer;
    private static string _logPath;

    internal static void Initialize()
    {
        try
        {
            lock (Sync)
            {
                if (_writer != null)
                {
                    return;
                }

                var assemblyLocation = typeof(Plugin).Assembly.Location;
                var pluginDirectory = Path.GetDirectoryName(assemblyLocation) ?? ".";
                _logPath = Path.Combine(pluginDirectory, "SubCraftica_PrototypeCompat.log");
                _writer = new StreamWriter(_logPath, append: true) { AutoFlush = true };

                WriteLine(LogLevel.Info, "=== Prototype compat debug session started ===");
                WriteLine(LogLevel.Info, $"SessionUtc={DateTime.UtcNow:O}");
                WriteLine(LogLevel.Info, $"AssemblyLocation={assemblyLocation}");
                WriteLine(LogLevel.Info, $"LogPath={_logPath}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogError($"[PrototypeCompat] Logger init failed: {ex.Message}");
        }
    }

    internal static void Debug(string message)
    {
        WriteLine(LogLevel.Debug, message);
    }

    internal static void Info(string message)
    {
        WriteLine(LogLevel.Info, message);
    }

    internal static void Warn(string message)
    {
        WriteLine(LogLevel.Warning, message);
    }

    internal static void Error(string message)
    {
        WriteLine(LogLevel.Error, message);
    }

    internal static void Error(Exception ex, string context)
    {
        if (ex == null)
        {
            WriteLine(LogLevel.Error, $"{context} | Exception=null");
            return;
        }

        WriteLine(LogLevel.Error, $"{context} | {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
    }

    internal static void Dispose()
    {
        lock (Sync)
        {
            if (_writer == null)
            {
                return;
            }

            try
            {
                WriteLine(LogLevel.Info, "=== Prototype compat debug session ended ===");
                _writer.Dispose();
            }
            catch
            {
            }
            finally
            {
                _writer = null;
            }
        }
    }

    private static void WriteLine(LogLevel level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}";

        try
        {
            Plugin.Log?.Log(level, $"[PrototypeCompat] {message}");
        }
        catch
        {
        }

        try
        {
            lock (Sync)
            {
                _writer?.WriteLine(line);
            }
        }
        catch
        {
        }
    }
}