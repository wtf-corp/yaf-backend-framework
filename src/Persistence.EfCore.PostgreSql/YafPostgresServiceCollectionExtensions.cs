using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace YAF.Persistence.EfCore.PostgreSql;

/// <summary>Connects a <see cref="YafDbContext"/> to PostgreSQL.</summary>
public static class YafPostgresServiceCollectionExtensions
{
    /// <summary>
    /// Registers <typeparamref name="TContext"/> with <see cref="YafDbContextServiceCollectionExtensions.AddYafDbContext{TContext}"/> on
    /// PostgreSQL over the connection string <c>ConnectionStrings:&lt;connectionName&gt;</c>: retry on transient failures, a command
    /// timeout of 30 seconds, PostgreSQL's error detail only in Development, and snake_case names for tables and columns, also the columns
    /// of complex types (<c>price_amount</c>). Calling it again for the same context registers nothing.
    /// </summary>
    /// <typeparam name="TContext">The service's context.</typeparam>
    /// <param name="builder">The host builder of the service. Its services must also hold an
    /// <see cref="YAF.Application.Context.IExecutionContextAccessor"/> and a <see cref="TimeProvider"/>.</param>
    /// <param name="connectionName">The name of the connection string.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// When the host starts it fails with an <see cref="Microsoft.Extensions.Options.OptionsValidationException"/> naming the key if the
    /// connection string is missing or, outside Development, does not set <c>SSL Mode=VerifyFull</c>. Development allows any SSL mode.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.AddYafPostgres&lt;CatalogDbContext&gt;("catalog");
    /// </code>
    /// </example>
    public static IHostApplicationBuilder AddYafPostgres<TContext>(this IHostApplicationBuilder builder, string connectionName)
        where TContext : YafDbContext
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionName);
        if (builder.Services.Any(descriptor => descriptor.ServiceType == typeof(TContext)))
        {
            return builder;
        }

        var configuration = builder.Configuration;
        var development = builder.Environment.IsDevelopment();
        var key = $"ConnectionStrings:{connectionName}";
        builder.Services.AddOptions<PostgresConnectionCheck>(typeof(TContext).FullName)
            .Validate(_ => configuration.GetConnectionString(connectionName) is { Length: > 0 }, $"{key} is missing.")
            .Validate(
                _ => development || configuration.GetConnectionString(connectionName) is not { Length: > 0 } connectionString
                    || new NpgsqlConnectionStringBuilder(connectionString).SslMode == SslMode.VerifyFull,
                $"{key} must set SSL Mode=VerifyFull outside Development.")
            .ValidateOnStart();
        builder.Services.AddYafDbContext<TContext>(options =>
        {
            var connection = new NpgsqlConnectionStringBuilder(
                configuration.GetConnectionString(connectionName) ?? throw new InvalidOperationException($"{key} is missing."))
            {
                IncludeErrorDetail = development,
            };
            options.UseNpgsql(connection.ConnectionString, npgsql => npgsql.EnableRetryOnFailure().CommandTimeout(30));
            ((IDbContextOptionsBuilderInfrastructure)options).AddOrUpdateExtension(new PostgresOptionsExtension());
        });
        return builder;
    }

    // The options type the startup check of the connection string is registered under; it holds nothing.
    private sealed class PostgresConnectionCheck;
}
