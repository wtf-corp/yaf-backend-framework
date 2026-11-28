using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace YAF.Persistence.EfCore.Conventions;

/// <summary>Adds YAF's conventions to the convention set of a context registered with <c>AddYafDbContext</c>.</summary>
internal sealed class YafConventionSetPlugin : IConventionSetPlugin
{
    public ConventionSet ModifyConventions(ConventionSet conventionSet)
    {
        conventionSet.Add(new YafModelCheckConvention());
        return conventionSet;
    }
}
