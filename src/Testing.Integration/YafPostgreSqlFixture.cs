using System.Data.Common;
using System.Security.Cryptography;
using Testcontainers.PostgreSql;
using Xunit;

namespace YAF.Testing.Integration;

/// <summary>
/// One PostgreSQL container for the tests that share this fixture, typically an xUnit collection (15 §3.2). Each caller creates a database
/// of its own with <see cref="CreateDatabaseAsync"/>; the container is removed when the fixture is disposed.
/// </summary>
/// <remarks>
/// xUnit starts the container through <see cref="InitializeAsync"/> before the first test of the collection; Docker must be running. The
/// superuser's password is generated for each fixture and never leaves the connection strings this fixture returns.
/// </remarks>
/// <example>
/// <code>
/// [CollectionDefinition("PostgreSql")]
/// public sealed class PostgreSqlCollection : ICollectionFixture&lt;YafPostgreSqlFixture&gt;;
///
/// [Collection("PostgreSql")]
/// public sealed class SessionStoreTests(YafPostgreSqlFixture postgres)
/// {
///     [Fact]
///     public async Task Add_ThenFind_RestoresTheSession()
///     {
///         var connectionString = await postgres.CreateDatabaseAsync("session_store");
///         // …
///     }
/// }
/// </code>
/// </example>
public sealed class YafPostgreSqlFixture : IAsyncLifetime
{
    /// <summary>The container image: the current PostgreSQL release, pinned in this one place.</summary>
    public const string Image = "postgres:19";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithPassword(RandomNumberGenerator.GetHexString(32, lowercase: true))
        .Build();

    /// <summary>Gets the connection string of the container's default database, as the superuser. Valid once the fixture has started.</summary>
    public string ConnectionString => _container.GetConnectionString();

    /// <summary>Creates a new, empty database in the container and returns its connection string, as the superuser.</summary>
    /// <param name="name">The database name: one or more of the characters <c>a</c>–<c>z</c>, <c>0</c>–<c>9</c> and <c>_</c>, unique
    /// within the container.</param>
    /// <returns>The connection string of the new database.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is empty or has another character.</exception>
    /// <exception cref="InvalidOperationException">PostgreSQL refused to create the database, for example because the name is taken; the
    /// message carries the server's error.</exception>
    public async Task<string> CreateDatabaseAsync(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!name.All(static c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_'))
        {
            throw new ArgumentException("A database name has only the characters a-z, 0-9 and _.", nameof(name));
        }

        // psql runs each statement of the file in its own transaction, which CREATE DATABASE requires (research §9); ON_ERROR_STOP makes
        // a failed statement a non-zero exit code.
        var result = await _container.ExecScriptAsync($"\\set ON_ERROR_STOP on\nCREATE DATABASE \"{name}\";\n").ConfigureAwait(false);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Creating database '{name}' failed: {result.Stderr}");
        }

        var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionString };
        builder["Database"] = name;
        return builder.ConnectionString;
    }

    /// <summary>Starts the container. xUnit calls this once, before the first test that uses the fixture.</summary>
    /// <returns>A task that completes when PostgreSQL accepts connections.</returns>
    public async ValueTask InitializeAsync() => await _container.StartAsync().ConfigureAwait(false);

    /// <summary>Stops and removes the container. xUnit calls this once, after the last test that uses the fixture.</summary>
    /// <returns>A task that completes when the container is gone.</returns>
    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
