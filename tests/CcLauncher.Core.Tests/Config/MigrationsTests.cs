using CcLauncher.Core.Config;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Xunit;

namespace CcLauncher.Core.Tests.Config;

public class MigrationsTests
{
    [Fact]
    public void Apply_OnEmptyDb_CreatesAllTablesAndRecordsVersion()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        Migrations.Apply(conn);

        AssertTableExists(conn, "ProjectSettings");
        AssertTableExists(conn, "GlobalSettings");
        AssertTableExists(conn, "LaunchHistory");
        AssertTableExists(conn, "SchemaVersion");

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT MAX(version) FROM SchemaVersion;";
        var v = Convert.ToInt64(cmd.ExecuteScalar());
        v.Should().Be(Migrations.CurrentVersion);
    }

    [Fact]
    public void Apply_IsIdempotent()
    {
        using var conn = new SqliteConnection("Data Source=:memory:");
        conn.Open();

        Migrations.Apply(conn);
        Migrations.Apply(conn); // second time should be a no-op

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM SchemaVersion;";
        var rows = Convert.ToInt64(cmd.ExecuteScalar());
        rows.Should().Be(Migrations.CurrentVersion);
    }

    private static void AssertTableExists(SqliteConnection conn, string table)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name=$n;";
        cmd.Parameters.AddWithValue("$n", table);
        var result = cmd.ExecuteScalar() as string;
        result.Should().NotBeNull();
    }
}
