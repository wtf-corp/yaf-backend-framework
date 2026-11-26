using YAF.Domain;

namespace Domain.Tests.Fixtures;

#region smart-enumeration
internal sealed record Channel : Enumeration<Channel>
{
    public static readonly Channel Web = new("web", "Web shop", requiresAddress: false);
    public static readonly Channel Phone = new("phone", "Phone order", requiresAddress: true);
    public static readonly Channel Store = new("store", "In store", requiresAddress: false);

    private Channel(string code, string name, bool requiresAddress)
        : base(code, name) => RequiresAddress = requiresAddress;

    public bool RequiresAddress { get; }
}
#endregion
