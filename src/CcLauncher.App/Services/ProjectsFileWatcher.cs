using System;
using System.IO;

namespace CcLauncher.App.Services;

public sealed class ProjectsFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly Action _onChange;
    private readonly System.Timers.Timer _debounce;

    public ProjectsFileWatcher(string projectsRoot, Action onChange)
    {
        _onChange = onChange;
        _debounce = new System.Timers.Timer(400) { AutoReset = false };
        _debounce.Elapsed += (_, _) => _onChange();

        if (!Directory.Exists(projectsRoot))
            Directory.CreateDirectory(projectsRoot);

        _watcher = new FileSystemWatcher(projectsRoot)
        {
            IncludeSubdirectories = true,
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.DirectoryName,
        };
        _watcher.Created += (_, _) => Bump();
        _watcher.Changed += (_, _) => Bump();
        _watcher.Deleted += (_, _) => Bump();
        _watcher.Renamed += (_, _) => Bump();
    }

    private void Bump()
    {
        _debounce.Stop();
        _debounce.Start();
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounce.Dispose();
    }
}
