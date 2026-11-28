using System.Reflection;
using Microsoft.EntityFrameworkCore;
using YAF.Core.Ids;
using YAF.Domain;
using YAF.Domain.ValueObjects;

namespace YAF.Persistence.EfCore.Conventions;

/// <summary>Maps the typed members of data classes (05 §1): typed ids, enums, smart enumerations and the shipped value objects.</summary>
internal static class TypedMemberConventions
{
    /// <summary>The maximum length of the column of an enum or a smart enumeration code.</summary>
    internal const int CodeMaxLength = 64;

    private const BindingFlags DeclaredMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Converts typed ids, enums, smart enumerations, <see cref="Currency"/>, <see cref="EmailAddress"/> and <see cref="Percentage"/>,
    /// with their nullable forms. Typed ids and smart enumerations are found on the properties of the types in
    /// <paramref name="dataAssembly"/>, the assembly that declares the data classes.
    /// </summary>
    public static void Configure(ModelConfigurationBuilder configurationBuilder, Assembly dataAssembly)
    {
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(CodeMaxLength);
        configurationBuilder.Properties<Currency>().HaveConversion<CurrencyConverter>().HaveMaxLength(3);
        configurationBuilder.Properties<EmailAddress>().HaveConversion<EmailAddressConverter>().HaveMaxLength(254);
        configurationBuilder.Properties<Percentage>().HaveConversion<PercentageConverter>();

        var memberTypes = dataAssembly.GetTypes()
            .SelectMany(type => type.GetProperties(DeclaredMembers))
            .Select(property => Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType)
            .Where(type => !type.ContainsGenericParameters)
            .Distinct();
        foreach (var type in memberTypes)
        {
            if (type.GetInterfaces().Any(contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition() == typeof(ITypedId<>)
                && contract.GetGenericArguments()[0] == type))
            {
                configurationBuilder.Properties(type).HaveConversion(typeof(TypedIdConverter<>).MakeGenericType(type));
            }
            else if (type.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(Enumeration<>)
                && baseType.GetGenericArguments()[0] == type)
            {
                configurationBuilder.Properties(type).HaveConversion(typeof(EnumerationConverter<>).MakeGenericType(type))
                    .HaveMaxLength(CodeMaxLength);
            }
        }
    }

    /// <summary>
    /// Maps every <see cref="Money"/>, <see cref="Period"/> and <see cref="DateRange"/> member of the model's entity types as a complex
    /// property with every component named: their components are get-only, so EF Core maps none of them unnamed (research §6).
    /// </summary>
    public static void MapComplexTypes(ModelBuilder modelBuilder)
    {
        var members = modelBuilder.Model.GetEntityTypes()
            .Where(entityType => !entityType.HasSharedClrType)
            .SelectMany(entityType => entityType.ClrType.GetProperties(DeclaredMembers), (entityType, property) => (entityType.ClrType, property))
            .ToList();
        foreach (var (clrType, property) in members)
        {
            string[] components = property.PropertyType == typeof(Money)
                ? [nameof(Money.Amount), nameof(Money.Currency)]
                : property.PropertyType == typeof(Period) || property.PropertyType == typeof(DateRange)
                    ? [nameof(Period.Start), nameof(Period.End)]
                    : [];
            if (components.Length > 0)
            {
                modelBuilder.Entity(clrType).ComplexProperty(property.PropertyType, property.Name, complex =>
                {
                    foreach (var component in components)
                    {
                        complex.Property(component);
                    }
                });
            }
        }
    }
}
