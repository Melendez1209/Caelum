using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;

namespace Caelum.Core.Data;

/// <summary>
///     Applies schema changes to databases created by earlier releases. The Windows app has shipped,
///     so an existing v1 database must gain the assistant columns instead of being replaced.
/// </summary>
/// <remarks>
///     EF Core migrations are deliberately not used: the schema is small and the project has no
///     design-time factory, so a versioned list of SQLite statements keeps the upgrade path
///     transparent. <c>PRAGMA user_version</c> records the schema version.
/// </remarks>
public static class DatabaseMigrator
{
    /// <summary>The schema version this build understands.</summary>
    public const int CurrentVersion = 2;

    /// <summary>
    ///     Creates the schema when the database is new, then brings an existing database up to
    ///     <see cref="CurrentVersion" />. Every statement is idempotent, so a partially upgraded
    ///     database can simply be visited again.
    /// </summary>
    public static async Task MigrateAsync(
        CaelumDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // A fresh database is created at the latest schema, so the migration only has to record the version
        await context.Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);

        var version = await GetUserVersionAsync(context, cancellationToken).ConfigureAwait(false);

        if (version >= CurrentVersion) return;

        if (version < 2)
            await AddMissingColumnsAsync(context, cancellationToken).ConfigureAwait(false);

        await SetUserVersionAsync(context, CurrentVersion, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<int> GetUserVersionAsync(
        CaelumDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();

        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version";

            var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

            return value is null or DBNull ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task SetUserVersionAsync(
        CaelumDbContext context,
        int version,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();

        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            using var command = connection.CreateCommand();

            // The version is a constant from this class, so interpolating it cannot inject SQL
            command.CommandText = $"PRAGMA user_version = {version}";

            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    /// <summary>
    ///     Adds the columns that were introduced after v1 only when they are missing. A fresh database
    ///     already has them, while a v1 database needs the grade and the assistant columns.
    /// </summary>
    private static async Task AddMissingColumnsAsync(
        CaelumDbContext context,
        CancellationToken cancellationToken)
    {
        var connection = context.Database.GetDbConnection();

        var wasOpen = connection.State == ConnectionState.Open;

        if (!wasOpen) await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var existing = await GetSettingsColumnsAsync(connection, cancellationToken).ConfigureAwait(false);

            if (!existing.Contains("Grade", StringComparer.OrdinalIgnoreCase))
            {
                await ExecuteAsync(
                    connection,
                    "ALTER TABLE settings ADD COLUMN Grade INTEGER NOT NULL DEFAULT 0",
                    cancellationToken).ConfigureAwait(false);
            }

            if (!existing.Contains("LlmProviderValue", StringComparer.OrdinalIgnoreCase))
            {
                await ExecuteAsync(
                    connection,
                    "ALTER TABLE settings ADD COLUMN LlmProviderValue INTEGER NOT NULL DEFAULT 0",
                    cancellationToken).ConfigureAwait(false);
            }

            if (!existing.Contains("LlmBaseUrl", StringComparer.OrdinalIgnoreCase))
            {
                await ExecuteAsync(
                    connection,
                    "ALTER TABLE settings ADD COLUMN LlmBaseUrl TEXT NOT NULL DEFAULT ''",
                    cancellationToken).ConfigureAwait(false);
            }

            if (!existing.Contains("LlmModel", StringComparer.OrdinalIgnoreCase))
            {
                await ExecuteAsync(
                    connection,
                    "ALTER TABLE settings ADD COLUMN LlmModel TEXT NOT NULL DEFAULT ''",
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            if (!wasOpen) await connection.CloseAsync().ConfigureAwait(false);
        }
    }

    private static async Task<HashSet<string>> GetSettingsColumnsAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(settings)";

        using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            if (reader.GetString(1) is { Length: > 0 } name)
                columns.Add(name);
        }

        return columns;
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
