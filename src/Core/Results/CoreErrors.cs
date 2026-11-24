namespace YAF.Core.Results;

/// <summary>The errors that <c>YAF.Core</c> itself reports.</summary>
public sealed class CoreErrors : IErrorCatalog
{
    /// <summary>
    /// The error of a result that was never initialised, such as <c>default(Result&lt;T&gt;)</c>: code <c>core.uninitialised</c>, kind
    /// <see cref="ErrorKind.Failure"/>. A result that was not created on purpose is never a success.
    /// </summary>
    public static readonly ErrorDescriptor Uninitialised = new("core.uninitialised", ErrorKind.Failure, "The result was not initialised.");

    private CoreErrors()
    {
    }
}
