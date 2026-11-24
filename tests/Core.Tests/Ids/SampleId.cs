using YAF.Core.Ids;

namespace Core.Tests.Ids;

// Written by hand: Core.Tests runs no typed id generator.
internal readonly record struct SampleId : ITypedId<SampleId>
{
    private SampleId(Guid value) => Value = value;

    public Guid Value { get; }

    public static SampleId From(Guid value) => new(value);
}
