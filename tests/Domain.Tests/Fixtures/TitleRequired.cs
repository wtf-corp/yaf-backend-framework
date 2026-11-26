using YAF.Core.Results;
using YAF.Domain.Rules;

namespace Domain.Tests.Fixtures;

internal sealed class TitleRequired(string title) : IBusinessRule
{
    public ErrorDescriptor Error => OrderErrors.TitleMissing;

    public bool IsSatisfied() => !string.IsNullOrWhiteSpace(title);
}
