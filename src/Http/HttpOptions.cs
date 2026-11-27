namespace YAF.Http;

/// <summary>
/// HTTP behaviour of a YAF service. Bound from the section <c>Http</c> by <see cref="YafHttpExtensions.AddYafHttp"/>, validated at startup.
/// </summary>
public sealed class HttpOptions
{
    /// <summary>
    /// Gets or sets the status of a problem document whose errors are all <see cref="YAF.Core.Results.ErrorKind.Validation"/>
    /// (key <c>Http:ValidationStatusCode</c>). The default is 400; 422 is the one other value allowed, any other value fails startup.
    /// </summary>
    public int ValidationStatusCode { get; set; } = 400;
}
