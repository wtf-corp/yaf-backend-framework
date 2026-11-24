namespace System.Runtime.CompilerServices;

/// <summary>
/// Lets records and <c>init</c> accessors compile on <c>netstandard2.0</c>, which lacks this type
/// (specs/0006-core/research.md §6). Internal, so it never clashes with the type of a newer framework.
/// </summary>
internal static class IsExternalInit;
