using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace YAF.Persistence.EfCore.PostgreSql;

/// <summary>
/// Turns the table and column names of the model, also the columns of complex types (<c>Price_Amount</c> → <c>price_amount</c>), into
/// snake_case (01 §2.1, 05 §2). Names configured by hand are left as they are.
/// </summary>
internal sealed class SnakeCaseNamingConvention : IModelFinalizingConvention
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

    /// <summary>Inserts an underscore before each word that starts with a capital letter and lower-cases the name.</summary>
    internal static string ToSnakeCase(string name)
    {
        var result = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var current = name[i];
            if (char.IsUpper(current) && i > 0 && name[i - 1] != '_' && StartsWord(name, i))
            {
                result.Append('_');
            }

            result.Append(char.ToLowerInvariant(current));
        }

        return result.ToString();
    }

    // A capital starts a word after a lower-case letter or a digit, and as the last capital of an acronym followed by a lower-case letter.
    private static bool StartsWord(string name, int index)
    {
        var previous = name[index - 1];
        return char.IsLower(previous) || char.IsDigit(previous)
            || (char.IsUpper(previous) && index + 1 < name.Length && char.IsLower(name[index + 1]));
    }

    // The properties declared on a type and, recursively, on its complex properties' types.
    private static IEnumerable<IConventionProperty> Properties(IConventionTypeBase type)
        => type.GetDeclaredProperties()
            .Concat(type.GetDeclaredComplexProperties().SelectMany(complexProperty => Properties(complexProperty.ComplexType)));
}
