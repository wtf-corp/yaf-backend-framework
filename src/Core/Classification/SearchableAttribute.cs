namespace YAF.Core.Classification;

/// <summary>
/// Marks a protected member that the store must find by equality. The member stays encrypted and gets a keyed-hash column,
/// which erasure clears. Sorting, ranges and prefixes do not work on it.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
public sealed class SearchableAttribute : Attribute;
