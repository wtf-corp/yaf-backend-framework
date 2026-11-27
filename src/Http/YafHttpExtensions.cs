using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace YAF.Http;

/// <summary>Sets up the HTTP side of a YAF service.</summary>
public static class YafHttpExtensions
{
    private const string HttpSection = "Http";

    private const string ProblemDetailsSection = "Http:ProblemDetails";

    /// <summary>
    /// Registers <see cref="IProblemDetailsMapper"/> and binds <see cref="HttpOptions"/> from the section <c>Http</c> and
    /// <see cref="ProblemDetailsOptions"/> from <c>Http:ProblemDetails</c>. Calling it again changes nothing.
    /// </summary>
    /// <remarks>
    /// Both options are validated when the host starts: a <c>Http:ValidationStatusCode</c> other than 400 or 422, or a
    /// <c>Http:ProblemDetails:TypeBaseUri</c> that is not an absolute URI, fails startup with an
    /// <see cref="Microsoft.Extensions.Options.OptionsValidationException"/> naming the key.
    /// </remarks>
    /// <example>
    /// <code language="csharp">
    /// var builder = WebApplication.CreateBuilder(args);
    /// builder.AddYafHttp();
    /// </code>
    /// </example>
    /// <param name="builder">The host builder of the service.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHostApplicationBuilder AddYafHttp(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (builder.Services.Any(service => service.ServiceType == typeof(IProblemDetailsMapper)))
        {
            return builder;
        }

        builder.Services.AddOptions<HttpOptions>()
            .Bind(builder.Configuration.GetSection(HttpSection))
            .Validate(options => options.ValidationStatusCode is 400 or 422, $"{HttpSection}:ValidationStatusCode must be 400 or 422.")
            .ValidateOnStart();
        builder.Services.AddOptions<ProblemDetailsOptions>()
            .Bind(builder.Configuration.GetSection(ProblemDetailsSection))
            .Validate(options => options.TypeBaseUri is { IsAbsoluteUri: true }, $"{ProblemDetailsSection}:TypeBaseUri must be an absolute URI.")
            .ValidateOnStart();
        builder.Services.AddSingleton<IProblemDetailsMapper, ProblemDetailsMapper>();
        return builder;
    }
}
