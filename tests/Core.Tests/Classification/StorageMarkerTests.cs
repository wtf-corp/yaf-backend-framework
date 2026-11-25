using System.Reflection;
using YAF.Core.Classification;

namespace Core.Tests.Classification;

public sealed class StorageMarkerTests
{
    [Fact]
    public void StoredInClear_Reason_IsKept()
    {
        var attribute = new StoredInClearAttribute("sorted attendee list");

        attribute.Reason.Should().Be("sorted attendee list");
    }

    [Fact]
    public void FieldId_Id_IsKept()
    {
        var attribute = new FieldIdAttribute("registration.attendee-email");

        attribute.Id.Should().Be("registration.attendee-email");
    }

    [Fact]
    public void Searchable_OnMember_IsRead()
    {
        var property = typeof(ISample).GetProperty(nameof(ISample.Email))!;

        property.GetCustomAttribute<SearchableAttribute>().Should().NotBeNull();
        property.GetCustomAttribute<FieldIdAttribute>()!.Id.Should().Be("sample.email");
    }

    [Fact]
    public void StoredInClear_OnMember_IsReadWithReason()
    {
        var property = typeof(ISample).GetProperty(nameof(ISample.Surname))!;

        property.GetCustomAttribute<StoredInClearAttribute>()!.Reason.Should().Be("sorted list");
    }

    private interface ISample
    {
        [PersonalData(PersonalDataCategory.Contact)]
        [Searchable]
        [FieldId("sample.email")]
        string Email { get; set; }

        [PersonalData(PersonalDataCategory.Identity)]
        [StoredInClear("sorted list")]
        string Surname { get; set; }
    }
}
