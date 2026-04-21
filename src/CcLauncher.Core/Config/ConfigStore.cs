using Microsoft.Data.Sqlite;

namespace CcLauncher.Core.Config;

public sealed class ConfigStore : IConfigStore, IDisposable
{
    private readonly SqliteConnection _conn;
    private readonly string _platformTerminal;

    private ConfigStore(SqliteConnection conn, string platformTerminal)
    {
        _conn = conn;
        _platformTerminal = platformTerminal;
        Migrations.Apply(_conn);
    }

    public static ConfigStore OpenFile(string path, string platformTerminal)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        var conn = new SqliteConnection($"Data Source={path}");
        conn.Open();
        return new ConfigStore(conn, platformTerminal);
    }

    public static ConfigStore OpenInMemory(string platformTerminal)
    {
        var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();
        return new ConfigStore(conn, platformTerminal);
    }

    public GlobalSettings GetGlobalSettings()
    {
        var kv = ReadAllGlobalKv();
        return new GlobalSettings(
            TerminalCommand:    kv.GetValueOrDefault("terminal_command") ?? _platformTerminal,
            GlobalDefaultArgs:  kv.GetValueOrDefault("global_default_args"),
            GlobalSystemPrompt: kv.GetValueOrDefault("global_system_prompt"),
            LaunchOnStartup:    kv.GetValueOrDefault("launch_on_startup") == "1",
            ResumeAllOnOpen:    kv.GetValueOrDefault("resume_all_on_open") == "1",
            CloseOnLaunch:      (kv.GetValueOrDefault("close_on_launch") ?? "1") == "1");
    }

    public void SaveGlobalSettings(GlobalSettings g)
    {
        WriteGlobalKv("terminal_command",     g.TerminalCommand);
        WriteGlobalKv("global_default_args",  g.GlobalDefaultArgs);
        WriteGlobalKv("global_system_prompt", g.GlobalSystemPrompt);
        WriteGlobalKv("launch_on_startup",    g.LaunchOnStartup ? "1" : "0");
        WriteGlobalKv("resume_all_on_open",   g.ResumeAllOnOpen ? "1" : "0");
        WriteGlobalKv("close_on_launch",      g.CloseOnLaunch ? "1" : "0");
    }

    public ProjectSettings GetProjectSettings(string projectId)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT display_name,pinned,hidden,default_args,system_prompt,last_launched_at FROM ProjectSettings WHERE project_id=$id;";
        cmd.Parameters.AddWithValue("$id", projectId);
        using var r = cmd.ExecuteReader();
        if (!r.Read()) return ProjectSettings.Default(projectId);
        return new ProjectSettings(
            ProjectId:      projectId,
            DisplayName:    r.IsDBNull(0) ? null : r.GetString(0),
            Pinned:         r.GetInt32(1) != 0,
            Hidden:         r.GetInt32(2) != 0,
            DefaultArgs:    r.IsDBNull(3) ? null : r.GetString(3),
            SystemPrompt:   r.IsDBNull(4) ? null : r.GetString(4),
            LastLaunchedAt: r.IsDBNull(5) ? null : DateTime.Parse(r.GetString(5)).ToUniversalTime());
    }

    public IReadOnlyDictionary<string, ProjectSettings> GetAllProjectSettings()
    {
        var dict = new Dictionary<string, ProjectSettings>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT project_id,display_name,pinned,hidden,default_args,system_prompt,last_launched_at FROM ProjectSettings;";
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            var id = r.GetString(0);
            dict[id] = new ProjectSettings(
                id,
                r.IsDBNull(1) ? null : r.GetString(1),
                r.GetInt32(2) != 0,
                r.GetInt32(3) != 0,
                r.IsDBNull(4) ? null : r.GetString(4),
                r.IsDBNull(5) ? null : r.GetString(5),
                r.IsDBNull(6) ? null : DateTime.Parse(r.GetString(6)).ToUniversalTime());
        }
        return dict;
    }

    public void SaveProjectSettings(ProjectSettings p)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ProjectSettings(project_id,display_name,pinned,hidden,default_args,system_prompt,last_launched_at)
            VALUES($id,$dn,$pin,$hid,$da,$sp,$ll)
            ON CONFLICT(project_id) DO UPDATE SET
                display_name=excluded.display_name,
                pinned=excluded.pinned,
                hidden=excluded.hidden,
                default_args=excluded.default_args,
                system_prompt=excluded.system_prompt,
                last_launched_at=excluded.last_launched_at;
            """;
        cmd.Parameters.AddWithValue("$id",  p.ProjectId);
        cmd.Parameters.AddWithValue("$dn",  (object?)p.DisplayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$pin", p.Pinned ? 1 : 0);
        cmd.Parameters.AddWithValue("$hid", p.Hidden ? 1 : 0);
        cmd.Parameters.AddWithValue("$da",  (object?)p.DefaultArgs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$sp",  (object?)p.SystemPrompt ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ll",  (object?)p.LastLaunchedAt?.ToString("O") ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public long InsertLaunch(string projectId, string sessionId, int? pid)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LaunchHistory(project_id, session_id, launched_at, pid)
            VALUES($pid, $sid, $at, $procPid);
            SELECT last_insert_rowid();
            """;
        cmd.Parameters.AddWithValue("$pid",     projectId);
        cmd.Parameters.AddWithValue("$sid",     sessionId);
        cmd.Parameters.AddWithValue("$at",      DateTime.UtcNow.ToString("O"));
        cmd.Parameters.AddWithValue("$procPid", (object?)pid ?? DBNull.Value);
        return Convert.ToInt64(cmd.ExecuteScalar());
    }

    public IReadOnlyList<LaunchHistoryEntry> GetRecentLaunches(int take)
    {
        var list = new List<LaunchHistoryEntry>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT id,session_id,project_id,launched_at,closed_at,pid FROM LaunchHistory ORDER BY launched_at DESC LIMIT $n;";
        cmd.Parameters.AddWithValue("$n", take);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            list.Add(new LaunchHistoryEntry(
                Id:         r.GetInt64(0),
                SessionId:  r.GetString(1),
                ProjectId:  r.GetString(2),
                LaunchedAt: DateTime.Parse(r.GetString(3)).ToUniversalTime(),
                ClosedAt:   r.IsDBNull(4) ? null : DateTime.Parse(r.GetString(4)).ToUniversalTime(),
                Pid:        r.IsDBNull(5) ? null : r.GetInt32(5)));
        }
        return list;
    }

    private Dictionary<string, string?> ReadAllGlobalKv()
    {
        var dict = new Dictionary<string, string?>();
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = "SELECT key, value FROM GlobalSettings;";
        using var r = cmd.ExecuteReader();
        while (r.Read()) dict[r.GetString(0)] = r.IsDBNull(1) ? null : r.GetString(1);
        return dict;
    }

    private void WriteGlobalKv(string key, string? value)
    {
        using var cmd = _conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO GlobalSettings(key,value) VALUES($k,$v)
            ON CONFLICT(key) DO UPDATE SET value=excluded.value;
            """;
        cmd.Parameters.AddWithValue("$k", key);
        cmd.Parameters.AddWithValue("$v", (object?)value ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    public void Dispose() => _conn.Dispose();
}
