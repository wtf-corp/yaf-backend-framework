using YAF.Domain;

namespace Persistence.EfCore.Tests.Conventions;

internal sealed record TicketFormat : Enumeration<TicketFormat>
{
    public static readonly TicketFormat Standard = new("standard", "Standard");

    private TicketFormat(string code, string name)
        : base(code, name)
    {
    }
}
