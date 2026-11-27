using YAF.Application;
using YAF.Core.Results;
using YAF.Domain.Events;
using YAF.Testing;

namespace Application.Tests.Contracts;

public sealed class RequestContractTests
{
    [Fact]
    public void Command_IsARequestOfItsResult()
    {
        object request = new RenameCommand();

        request.Should().BeAssignableTo<IRequest<Result>>();
    }

    [Fact]
    public void Query_IsARequestOfItsResult()
    {
        object request = new NameQuery();

        request.Should().BeAssignableTo<IRequest<Result<string>>>();
    }

    [Fact]
    public void Handler_IsContravariantInTheRequest()
    {
        object handler = new AnyRequestHandler();

        handler.Should().BeAssignableTo<IRequestHandler<RenameCommand, Result>>();
    }

    [Fact]
    public async Task DomainEventHandler_IsContravariantInTheEvent()
    {
        var handler = (IDomainEventHandler<SessionRenamed>)(object)new AnyDomainEventHandler();

        var result = await handler.HandleAsync(new SessionRenamed(), TestContext.Current.CancellationToken);

        result.ShouldSucceed();
    }

    [Fact]
    public async Task StreamQueryHandler_IsVariantInQueryAndItem()
    {
        var handler = (IStreamQueryHandler<NamesQuery, object>)(object)new NamesHandler();

        var items = await handler.HandleAsync(new NamesQuery(), TestContext.Current.CancellationToken).ToListAsync(TestContext.Current.CancellationToken);

        items.Should().Equal("speaker-0001");
    }

    private sealed record RenameCommand : ICommand<Result>;

    private sealed record NameQuery : IQuery<Result<string>>;

    private sealed record NamesQuery : IStreamQuery<string>;

    private sealed record SessionRenamed : DomainEvent;

    private sealed class AnyRequestHandler : IRequestHandler<IRequest<Result>, Result>
    {
        public ValueTask<Result> HandleAsync(IRequest<Result> request, CancellationToken ct) => ValueTask.FromResult(Result.Success());
    }

    private sealed class AnyDomainEventHandler : IDomainEventHandler<IDomainEvent>
    {
        public ValueTask<Result> HandleAsync(IDomainEvent domainEvent, CancellationToken ct) => ValueTask.FromResult(Result.Success());
    }

    private sealed class NamesHandler : IStreamQueryHandler<IStreamQuery<string>, string>
    {
        private static readonly string[] s_names = ["speaker-0001"];

        public IAsyncEnumerable<string> HandleAsync(IStreamQuery<string> query, CancellationToken ct) => s_names.ToAsyncEnumerable();
    }
}
