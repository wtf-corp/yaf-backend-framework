using YAF.Core.Results;

namespace YAF.Application.Errors;

/// <summary>
/// One error of the catalogue that <see cref="IErrorCatalogProvider"/> lists: the descriptor and the type that declares it, so that a code
/// can be traced to its declaration and a duplicate code reported with both declaring types.
/// </summary>
/// <param name="Descriptor">The descriptor, with its code, kind and message template.</param>
/// <param name="DeclaringType">The full name of the <see cref="IErrorCatalog"/> type that declares the descriptor, for example
/// <c>Catalog.Domain.Session+Errors</c>.</param>
public sealed record ErrorCatalogEntry(ErrorDescriptor Descriptor, string DeclaringType);
