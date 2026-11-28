using YAF.Domain.ValueObjects;

namespace Persistence.EfCore.Tests.Conventions;

// One member of every type the conventions map, none of them configured by hand.
internal sealed class TicketData
{
    public Guid Id { get; set; }

    public TicketId Owner { get; set; }

    public TicketId? Previous { get; set; }

    public TicketFormat Format { get; set; } = TicketFormat.Standard;

    public TicketStatus Status { get; set; }

    public Money Price { get; set; } = null!;

    public Period? Schedule { get; set; }

    public DateRange Validity { get; set; } = null!;

    public EmailAddress Contact { get; set; } = null!;

    public Percentage Discount { get; set; } = null!;
}
