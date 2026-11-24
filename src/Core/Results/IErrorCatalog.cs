namespace YAF.Core.Results;

/// <summary>
/// Marks a class that declares errors. Each error is a <c>static readonly</c> <see cref="ErrorDescriptor"/> field initialised by one of the
/// factories of <see cref="Error"/>, such as <see cref="Error.NotFound(string)"/>.
/// </summary>
/// <remarks>
/// By convention the catalogue is a nested class named <c>Errors</c> of the type that returns the errors. The error code generator fills the
/// code of each field from the MSBuild property <c>YafServiceCode</c>, the declaring type path and the field name.
/// </remarks>
/// <example>
/// <code language="csharp">
/// public partial class Session
/// {
///     internal sealed class Errors : IErrorCatalog
///     {
///         public static readonly ErrorDescriptor Full = Error.Conflict("Session has no free seats (capacity {Capacity}).");
///     }
/// }
/// </code>
/// </example>
public interface IErrorCatalog;
