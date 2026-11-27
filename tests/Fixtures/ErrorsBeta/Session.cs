using YAF.Core.Results;

namespace ErrorsBeta;

internal static class Session
{
    internal abstract class Errors : IErrorCatalog
    {
        public static readonly ErrorDescriptor NotFound = Error.NotFound("The session was not found.");
    }
}
