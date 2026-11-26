using YAF.Core.Results;
using YAF.Domain.Rules;

namespace Testing.Tests.Fixtures;

internal sealed class TitleRequired(string title) : IBusinessRule
{
    public ErrorDescriptor Error => BookingErrors.TitleMissing;

    public bool IsSatisfied() => !string.IsNullOrWhiteSpace(title);
}
