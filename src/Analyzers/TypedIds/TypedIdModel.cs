namespace YAF.Analyzers.TypedIds;

/// <summary>
/// What the generator needs of one typed id. A record of strings, so the incremental pipeline compares it by value and
/// regenerates only when the namespace or the name changes.
/// </summary>
/// <param name="Namespace">The containing namespace, or <see langword="null"/> for the global namespace.</param>
/// <param name="Name">The name of the id type.</param>
internal sealed record TypedIdModel(string? Namespace, string Name)
{
    /// <summary>Gets the name of the generated file, unique per id type.</summary>
    public string HintName => Namespace is null ? $"{Name}.TypedId.g.cs" : $"{Namespace}.{Name}.TypedId.g.cs";
}
