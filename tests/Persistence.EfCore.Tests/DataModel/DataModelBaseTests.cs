using System.Reflection;
using YAF.Domain.Mementos;
using YAF.Persistence.EfCore;

namespace Persistence.EfCore.Tests.DataModel;

public sealed class DataModelBaseTests
{
    [Fact]
    public void AggregateData_ImplementsTheAggregateMemento()
    {
        var type = typeof(AggregateData);

        type.IsAbstract.Should().BeTrue();
        type.Should().Implement<IAggregateMemento>();
        SettableProperties(type).Should().BeEquivalentTo(
        [
            new Member("Id", typeof(Guid)),
            new Member("Version", typeof(Guid)),
            new Member("TenantId", typeof(Guid)),
            new Member("CreatedAt", typeof(DateTimeOffset)),
            new Member("CreatedBy", typeof(Guid)),
            new Member("ModifiedAt", typeof(DateTimeOffset)),
            new Member("ModifiedBy", typeof(Guid)),
        ]);
    }

    [Fact]
    public void AggregateData_SetThroughTheMemento_ReadsBack()
    {
        var at = new DateTimeOffset(2026, 11, 27, 9, 30, 0, TimeSpan.Zero);
        IAggregateMemento memento = new FixtureAggregateData();

        memento.Id = Guid.Parse("0198a3c2-0000-7000-8000-000000000001");
        memento.Version = Guid.Parse("0198a3c2-0000-7000-8000-000000000002");
        memento.TenantId = Guid.Parse("0198a3c2-0000-7000-8000-000000000003");
        memento.CreatedAt = at;
        memento.CreatedBy = Guid.Parse("0198a3c2-0000-7000-8000-000000000004");
        memento.ModifiedAt = at.AddHours(1);
        memento.ModifiedBy = Guid.Parse("0198a3c2-0000-7000-8000-000000000005");

        var data = (AggregateData)memento;
        data.Should().BeEquivalentTo(new
        {
            Id = Guid.Parse("0198a3c2-0000-7000-8000-000000000001"),
            Version = Guid.Parse("0198a3c2-0000-7000-8000-000000000002"),
            TenantId = Guid.Parse("0198a3c2-0000-7000-8000-000000000003"),
            CreatedAt = at,
            CreatedBy = Guid.Parse("0198a3c2-0000-7000-8000-000000000004"),
            ModifiedAt = at.AddHours(1),
            ModifiedBy = Guid.Parse("0198a3c2-0000-7000-8000-000000000005"),
        });
    }

    [Fact]
    public void OwnedEntityData_HasNoTenantAndNoVersion()
    {
        var type = typeof(OwnedEntityData);

        type.IsAbstract.Should().BeTrue();
        type.Should().Implement<IOwnedEntityMemento>();
        type.Should().NotImplement<ITenantOwnedMemento>();
        type.Should().NotImplement<IVersionedMemento>();
        SettableProperties(type).Should().BeEquivalentTo(
        [
            new Member("Id", typeof(Guid)),
            new Member("CreatedAt", typeof(DateTimeOffset)),
            new Member("CreatedBy", typeof(Guid)),
            new Member("ModifiedAt", typeof(DateTimeOffset)),
            new Member("ModifiedBy", typeof(Guid)),
        ]);
    }

    [Fact]
    public void MementoData_AppliesToClassesOnly()
    {
        var usage = typeof(MementoDataAttribute).GetCustomAttribute<AttributeUsageAttribute>();

        typeof(MementoDataAttribute).IsSealed.Should().BeTrue();
        usage.Should().BeEquivalentTo(new { ValidOn = AttributeTargets.Class, AllowMultiple = false, Inherited = false });
    }

    private static IEnumerable<Member> SettableProperties(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.CanRead && property.SetMethod is { IsPublic: true })
            .Select(property => new Member(property.Name, property.PropertyType));

    private sealed record Member(string Name, Type Type);

    private sealed class FixtureAggregateData : AggregateData;
}
