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
    public void StorageMarkers_OnMember_AreReadTogether()
    {
        var property = typeof(ISample).GetProperty(nameof(ISample.Surname))!;

        property.GetCustomAttribute<SearchableAttribute>().Should().NotBeNull();
        property.GetCustomAttribute<StoredInClearAttribute>()!.Reason.Should().Be("sorted list");
        property.GetCustomAttribute<FieldIdAttribute>()!.Id.Should().Be("sample.surname");
    }

    private interface ISample
    {
        [PersonalData(PersonalDataCategory.Identity)]
        [Searchable]
        [StoredInClear("sorted list")]
        [FieldId("sample.surname")]
        string Surname { get; set; }
    }
}
