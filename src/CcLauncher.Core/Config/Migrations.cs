using Microsoft.Data.Sqlite;

namespace CcLauncher.Core.Config;

public static class Migrations
{
    public const int CurrentVersion = 1;

    private static readonly string[] Scripts =
    {
        // version 1 — initial schema
        """
        CREATE TABLE ProjectSettings (
            project_id       TEXT PRIMARY KEY,
            display_name     TEXT,
            pinned           INTEGER NOT NULL DEFAULT 0,
            hidden           INTEGER NOT NULL DEFAULT 0,
            default_args     TEXT,
            system_prompt    TEXT,
            last_launched_at TEXT
        );

        CREATE TABLE GlobalSettings (
            key   TEXT PRIMARY KEY,
            value TEXT
        );

        CREATE TABLE LaunchHistory (
            id          INTEGER PRIMARY KEY AUTOINCREMENT,
            session_id  TEXT NOT NULL,
            project_id  TEXT NOT NULL,
            launched_at TEXT NOT NULL,
            closed_at   TEXT,
            pid         INTEGER
        );

        CREATE INDEX idx_launch_history_project ON LaunchHistory(project_id, launched_at DESC);
        """,
    };

    public static void Apply(SqliteConnection connection)
    {
        EnsureVersionTable(connection);
        var current = GetCurrentVersion(connection);

        for (var i = current; i < CurrentVersion; i++)
        {
            using var tx = connection.BeginTransaction();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = Scripts[i];
                cmd.ExecuteNonQuery();
            }

            using (var vCmd = connection.CreateCommand())
            {
                vCmd.Transaction = tx;
                vCmd.CommandText = "INSERT INTO SchemaVersion(version, applied_at) VALUES ($v, $t);";
                vCmd.Parameters.AddWithValue("$v", i + 1);
                vCmd.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
                vCmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    private static void EnsureVersionTable(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                version    INTEGER PRIMARY KEY,
                applied_at TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static int GetCurrentVersion(SqliteConnection conn)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM SchemaVersion;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }
}
