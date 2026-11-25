using YAF.Domain.Mementos;

namespace Domain.Tests.Mementos;

public sealed class MementoContractTests
{
    [Fact]
    public void AggregateMemento_Inherits_IdVersionTenantAndStamps()
    {
        var properties = InheritedProperties(typeof(IAggregateMemento));

        properties.Should().BeEquivalentTo(
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
    public void OwnedEntityMemento_HasNoTenantAndNoVersion()
    {
        var properties = InheritedProperties(typeof(IOwnedEntityMemento));

        properties.Should().BeEquivalentTo(
        [
            new Member("Id", typeof(Guid)),
            new Member("CreatedAt", typeof(DateTimeOffset)),
            new Member("CreatedBy", typeof(Guid)),
            new Member("ModifiedAt", typeof(DateTimeOffset)),
            new Member("ModifiedBy", typeof(Guid)),
        ]);
    }

    [Fact]
    public void SoftDeletableMemento_HasNullableDeletionStamps()
    {
        var properties = InheritedProperties(typeof(ISoftDeletableMemento));

        properties.Should().BeEquivalentTo(
        [
            new Member("DeletedAt", typeof(DateTimeOffset?)),
            new Member("DeletedBy", typeof(Guid?)),
        ]);
    }

    [Fact]
    public void DataSubjectOwnedMemento_HasDataSubjectId()
    {
        var properties = InheritedProperties(typeof(IDataSubjectOwnedMemento));

        properties.Should().BeEquivalentTo([new Member("DataSubjectId", typeof(Guid))]);
    }

    [Fact]
    public void CorruptStateException_KeepsMessageAndInner()
    {
        var inner = new InvalidOperationException("inner");

        var exception = new CorruptStateException("Order 0198a3c2 has a memento of another id.", inner);

        exception.Message.Should().Be("Order 0198a3c2 has a memento of another id.");
        exception.InnerException.Should().BeSameAs(inner);
    }

    private static IEnumerable<Member> InheritedProperties(Type contract) =>
        contract.GetInterfaces()
            .Append(contract)
            .SelectMany(type => type.GetProperties())
            .Select(property => new Member(property.Name, property.PropertyType, property.CanRead && property.CanWrite));

    private sealed record Member(string Name, Type Type, bool HasGetAndSet = true);
}
