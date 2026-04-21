namespace CcLauncher.Core.Config;

public interface IConfigStore
{
    GlobalSettings GetGlobalSettings();
    void SaveGlobalSettings(GlobalSettings settings);

    ProjectSettings GetProjectSettings(string projectId);
    IReadOnlyDictionary<string, ProjectSettings> GetAllProjectSettings();
    void SaveProjectSettings(ProjectSettings settings);

    long InsertLaunch(string projectId, string sessionId, int? pid);
    IReadOnlyList<LaunchHistoryEntry> GetRecentLaunches(int take);
}
