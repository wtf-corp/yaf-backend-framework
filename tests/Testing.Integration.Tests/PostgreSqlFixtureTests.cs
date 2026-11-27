using Npgsql;
using YAF.Testing.Integration;

namespace Testing.Integration.Tests;

// A class fixture: the class is its own xUnit collection, so its tests share one container. A collection definition shared by several
// classes must be public, which CA1515 refuses in a test project (an executable).
[Trait("Category", "Docker")]
public sealed class PostgreSqlFixtureTests(YafPostgreSqlFixture fixture) : IClassFixture<YafPostgreSqlFixture>
{
    [Fact]
    public async Task CreateDatabase_ReturnsAConnectableDatabase()
    {
        var connectionString = await fixture.CreateDatabaseAsync("connectable").ConfigureAwait(true);

        var (current, _) = await CatalogAsync(connectionString).ConfigureAwait(true);

        current.Should().Be("connectable");
    }

    [Fact]
    public async Task Fixture_StartsOneContainer()
    {
        var first = await fixture.CreateDatabaseAsync("one_container_a").ConfigureAwait(true);
        await fixture.CreateDatabaseAsync("one_container_b").ConfigureAwait(true);

        var (_, databases) = await CatalogAsync(first).ConfigureAwait(true);

        databases.Should().Contain(["one_container_a", "one_container_b"]);
    }

    [Fact]
    public async Task CreateDatabase_ExistingName_ThrowsWithTheServersMessage()
    {
        await fixture.CreateDatabaseAsync("existing").ConfigureAwait(true);

        var create = () => fixture.CreateDatabaseAsync("existing");

        await create.Should().ThrowAsync<InvalidOperationException>().WithMessage("*already exists*").ConfigureAwait(true);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Upper")]
    [InlineData("x; DROP DATABASE postgres")]
    public async Task CreateDatabase_NameOutsideTheAllowedCharacters_Throws(string name)
    {
        var create = () => fixture.CreateDatabaseAsync(name);

        await create.Should().ThrowAsync<ArgumentException>().ConfigureAwait(true);
    }

    // The database the connection string opens, and every database of the server it reaches (pg_database is shared by the cluster).
    private static async Task<(string Current, string[] All)> CatalogAsync(string connectionString)
    {
        var connection = new NpgsqlConnection(connectionString);
        await using (connection.ConfigureAwait(true))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
            var command = new NpgsqlCommand("SELECT current_database(), array_agg(datname::text) FROM pg_database", connection);
            await using (command.ConfigureAwait(true))
            {
                var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                await using (reader.ConfigureAwait(true))
                {
                    await reader.ReadAsync(TestContext.Current.CancellationToken).ConfigureAwait(true);
                    return (reader.GetString(0), await reader.GetFieldValueAsync<string[]>(1, TestContext.Current.CancellationToken).ConfigureAwait(true));
                }
            }
        }
    }
}
