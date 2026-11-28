using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace YAF.Persistence.EfCore.PostgreSql;

/// <summary>
/// Turns the table and column names of the model, also the columns of complex types (<c>Price_Amount</c> → <c>price_amount</c>), into
/// snake_case (01 §2.1, 05 §2). Names configured by hand are left as they are.
/// </summary>
internal sealed partial class SnakeCaseNamingConvention : IModelFinalizingConvention
{
    public void ProcessModelFinalizing(IConventionModelBuilder modelBuilder, IConventionContext<IConventionModelBuilder> context)
    {
        foreach (var entityType in modelBuilder.Metadata.GetEntityTypes())
        {
            if (entityType.GetTableName() is { } table)
            {
                entityType.SetTableName(ToSnakeCase(table));
            }

            // The name in the table carries the prefix of a complex property (Price_Amount); the base name does not (Amount).
            var storeObject = StoreObjectIdentifier.Create(entityType, StoreObjectType.Table);
            foreach (var property in Properties(entityType))
            {
                if ((storeObject is { } tableObject ? property.GetColumnName(tableObject) : property.GetColumnName()) is { } column)
                {
                    property.SetColumnName(ToSnakeCase(column));
                }
            }
        }
    }

    /// <summary>Inserts an underscore where a word starts with a capital letter and lower-cases the name.</summary>
    internal static string ToSnakeCase(string name) => string.Concat(WordStart().Replace(name, "_").Select(char.ToLowerInvariant));

    // A capital starts a word after a lower-case letter or a digit, and as the last capital of an acronym before a lower-case letter.
    [GeneratedRegex("(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WordStart();

    // The properties declared on a type and, recursively, on its complex properties' types.
    private static IEnumerable<IConventionProperty> Properties(IConventionTypeBase type)
        => type.GetDeclaredProperties()
            .Concat(type.GetDeclaredComplexProperties().SelectMany(complexProperty => Properties(complexProperty.ComplexType)));
}
