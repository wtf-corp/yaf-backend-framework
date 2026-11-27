namespace YAF.Http;

/// <summary>
/// How <see cref="IProblemDetailsMapper"/> writes the <c>type</c> of a problem document. Bound from the section <c>Http:ProblemDetails</c>
/// by <see cref="YafHttpExtensions.AddYafHttp(Microsoft.Extensions.Hosting.IHostApplicationBuilder)"/> and validated at startup.
/// </summary>
public sealed class ProblemDetailsOptions
{
    /// <summary>
    /// Gets or sets the absolute URI that the error code is appended to, giving the <c>type</c> of a problem document
    /// (key <c>Http:ProblemDetails:TypeBaseUri</c>). The default is <c>urn:yaf:error:</c>; with <c>https://errors.example.com/</c> the
    /// error <c>catalog.session.full</c> has the type <c>https://errors.example.com/catalog.session.full</c>. A relative URI fails startup.
    /// </summary>
    public Uri TypeBaseUri { get; set; } = new("urn:yaf:error:");
}
