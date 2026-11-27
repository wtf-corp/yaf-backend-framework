using YAF.Core.Results;

namespace ErrorsAlpha;

internal static class Session
{
    internal abstract class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor NotFound = Error.NotFound("The session was not found.");
    }
}
