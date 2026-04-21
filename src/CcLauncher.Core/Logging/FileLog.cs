using System;
using System.IO;

namespace CcLauncher.Core.Logging;

public static class FileLog
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly object _lock = new();
    private static string? _path;

    public static void Initialize(string logsDir)
    {
        Directory.CreateDirectory(logsDir);
        _path = Path.Combine(logsDir, "app.log");
    }

    public static void Write(string level, string message, Exception? ex = null)
    {
        if (_path is null) return;
        lock (_lock)
        {
            try
            {
                if (File.Exists(_path) && new FileInfo(_path).Length > MaxBytes)
                    File.Move(_path, _path + ".1", overwrite: true);
                var line = $"{DateTime.UtcNow:O} [{level}] {message}" +
                           (ex is null ? "" : $"\n{ex}") + "\n";
                File.AppendAllText(_path, line);
            }
            catch { /* swallow — logging failures must never crash the app */ }
        }
    }

    public static void Info(string m)                      => Write("INFO", m);
    public static void Warn(string m, Exception? e = null) => Write("WARN", m, e);
    public static void Error(string m, Exception? e = null) => Write("ERROR", m, e);
}
