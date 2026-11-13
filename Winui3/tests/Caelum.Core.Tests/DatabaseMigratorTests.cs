using Caelum.Core.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Caelum.Core.Tests;

/// <summary>
///     Verifies the schema upgrade path for databases that were created before the assistant
///     existed, so an existing installation keeps its exams and settings.
/// </summary>
public sealed class DatabaseMigratorTests : IAsyncLifetime
{
    private SqliteConnection _keepAlive = null!;
    private CaelumDbContext _context = null!;

    public async Task InitializeAsync()
    {
        _keepAlive = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = $"caelum-test-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString());

        await _keepAlive.OpenAsync();

        _context = CaelumDatabase.CreateContextFor(_keepAlive.ConnectionString);
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _keepAlive.DisposeAsync();
    }

    [Fact]
    public async Task MigratingAFreshDatabaseCreatesTheMissingColumns()
    {
        await DatabaseMigrator.MigrateAsync(_context);

        var columns = await GetSettingsColumnsAsync();

        Assert.Contains("Grade", columns);
        Assert.Contains("LlmProviderValue", columns);
        Assert.Contains("LlmBaseUrl", columns);
        Assert.Contains("LlmModel", columns);
        Assert.Equal(DatabaseMigrator.CurrentVersion, await GetUserVersionAsync());
    }

    [Fact]
    public async Task MigratingAnExistingV1DatabaseAddsTheMissingColumns()
    {
        // A v1 database has the settings table as it was before the grade and assistant columns
        await ExecuteAsync(@"
            CREATE TABLE settings (
                Id INTEGER NOT NULL PRIMARY KEY,
                AppThemeValue INTEGER NOT NULL DEFAULT 0,
                IsHighContrastMode INTEGER NOT NULL DEFAULT 0,
                SelectedLanguage TEXT NOT NULL DEFAULT '',
                IsFirstLogin INTEGER NOT NULL DEFAULT 1,
                Identity INTEGER NOT NULL DEFAULT 0,
                Region TEXT NOT NULL DEFAULT '',
                SelectedSubjects TEXT NOT NULL DEFAULT '',
                TrendMetric INTEGER NOT NULL DEFAULT 0,
                SubjectMetric INTEGER NOT NULL DEFAULT 0,
                HistoryViewMode INTEGER NOT NULL DEFAULT 1
            );");
        await ExecuteAsync("PRAGMA user_version = 1;");

        await DatabaseMigrator.MigrateAsync(_context);

        var columns = await GetSettingsColumnsAsync();

        Assert.Contains("Grade", columns);
        Assert.Contains("LlmProviderValue", columns);
        Assert.Contains("LlmBaseUrl", columns);
        Assert.Contains("LlmModel", columns);
        Assert.Equal(DatabaseMigrator.CurrentVersion, await GetUserVersionAsync());
    }

    [Fact]
    public async Task MigratingTwiceIsHarmless()
    {
        await DatabaseMigrator.MigrateAsync(_context);
        await DatabaseMigrator.MigrateAsync(_context);

        Assert.Equal(DatabaseMigrator.CurrentVersion, await GetUserVersionAsync());
    }

    private async Task<HashSet<string>> GetSettingsColumnsAsync()
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = _keepAlive.CreateCommand();
        command.CommandText = "PRAGMA table_info(settings)";

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            if (reader.GetString(1) is { Length: > 0 } name)
                columns.Add(name);
        }

        return columns;
    }

    private async Task<int> GetUserVersionAsync()
    {
        using var command = _keepAlive.CreateCommand();
        command.CommandText = "PRAGMA user_version";

        var value = await command.ExecuteScalarAsync();

        return value is null or DBNull ? 0 : Convert.ToInt32(value);
    }

    private async Task ExecuteAsync(string commandText)
    {
        using var command = _keepAlive.CreateCommand();
        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync();
    }
}
