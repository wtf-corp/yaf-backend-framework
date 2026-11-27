namespace YAF.Core.Results;

/// <summary>
/// One error of an <see cref="IErrorRegistry"/>: the descriptor and where it is declared, so that a duplicate code can be reported with
/// both declaring types.
/// </summary>
/// <param name="Descriptor">The descriptor read from the field, with its code, kind and message template.</param>
/// <param name="DeclaringType">The full name of the <see cref="IErrorCatalog"/> type that declares the field, for example
/// <c>Catalog.Domain.Session+Errors</c>.</param>
/// <param name="Field">The name of the field, for example <c>Full</c>.</param>
public sealed record ErrorRegistration(ErrorDescriptor Descriptor, string DeclaringType, string Field);
