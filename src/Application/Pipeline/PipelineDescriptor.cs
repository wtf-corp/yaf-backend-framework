using System.Globalization;
using System.Text;

namespace YAF.Application.Pipeline;

internal sealed class PipelineDescriptor(IReadOnlyList<PipelineStep> steps) : IPipelineDescriptor
{
    public IReadOnlyList<PipelineStep> Steps { get; } = steps;

    public string Render()
    {
        var text = new StringBuilder();
        for (var i = 0; i < Steps.Count; i++)
        {
            var name = Steps[i].Middleware.Name;
            var arity = name.IndexOf('`', StringComparison.Ordinal);
            text.Append(CultureInfo.InvariantCulture, $"{i + 1}. {(arity < 0 ? name : name[..arity])} ({ScopeName(Steps[i].AppliesTo)})\n");
        }

        return text.ToString();
    }

    private static string ScopeName(PipelineScope scope) => scope switch
    {
        PipelineScope.Commands => "commands",
        PipelineScope.Queries => "queries",
        _ => "all",
    };
}
