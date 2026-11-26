using System.Linq.Expressions;
using Domain.Tests.Fixtures;
using YAF.Domain.Specifications;
using YAF.Domain.ValueObjects;

namespace Domain.Tests.Specifications;

public sealed class SpecificationTests
{
    private static readonly Currency s_eur = Currency.FromCode("EUR").Value;

    [Fact]
    public void And_BothMustHold()
    {
        var spec = new PlacedOrders().And(new OrdersThrough(Channel.Web));

        spec.IsSatisfiedBy(Summary(OrderStatus.Placed, Channel.Web)).Should().BeTrue();
        spec.IsSatisfiedBy(Summary(OrderStatus.Placed, Channel.Phone)).Should().BeFalse();
        spec.IsSatisfiedBy(Summary(OrderStatus.Draft, Channel.Web)).Should().BeFalse();
    }

    [Fact]
    public void Or_EitherHolds()
    {
        var spec = new PlacedOrders().Or(new OrdersThrough(Channel.Web));

        spec.IsSatisfiedBy(Summary(OrderStatus.Draft, Channel.Web)).Should().BeTrue();
        spec.IsSatisfiedBy(Summary(OrderStatus.Placed, Channel.Phone)).Should().BeTrue();
        spec.IsSatisfiedBy(Summary(OrderStatus.Draft, Channel.Phone)).Should().BeFalse();
    }

    [Fact]
    public void Not_Inverts()
    {
        var spec = new PlacedOrders().Not();

        spec.IsSatisfiedBy(Summary(OrderStatus.Draft, Channel.Web)).Should().BeTrue();
        spec.IsSatisfiedBy(Summary(OrderStatus.Placed, Channel.Web)).Should().BeFalse();
    }

    [Fact]
    public void IsSatisfiedBy_TypedMembers_ComparesDomainTypes()
    {
        var id = OrderId.New();
        var spec = new OrderWithId(id).And(new PlacedOrders()).And(new OrdersThrough(Channel.Store)).And(new TotalAbove(new Money(10m, s_eur)));
        var match = new OrderSummary(id, OrderStatus.Placed, Channel.Store, new Money(10.01m, s_eur));

        spec.IsSatisfiedBy(match).Should().BeTrue();
        spec.IsSatisfiedBy(match with { Id = OrderId.New() }).Should().BeFalse();
        spec.IsSatisfiedBy(match with { Status = OrderStatus.Cancelled }).Should().BeFalse();
        spec.IsSatisfiedBy(match with { Channel = Channel.Web }).Should().BeFalse();
        spec.IsSatisfiedBy(match with { Total = new Money(10m, s_eur) }).Should().BeFalse();
        spec.IsSatisfiedBy(match with { Total = new Money(10.01m, Currency.FromCode("USD").Value) }).Should().BeFalse();
    }

    [Fact]
    public void And_ComposedExpression_HasOneParameter()
    {
        var spec = new PlacedOrders().And(new OrdersThrough(Channel.Web)).Or(new TotalAbove(new Money(5m, s_eur)).Not());

        var expression = spec.ToExpression();

        var parameter = expression.Parameters.Should().ContainSingle().Subject;
        ParameterCollector.Collect(expression.Body).Should().Equal(parameter);
    }

    [Fact]
    public void IsSatisfiedBy_CalledTwice_CompilesOnce()
    {
        var spec = new CountingSpecification();

        spec.IsSatisfiedBy(Summary(OrderStatus.Placed, Channel.Web));
        spec.IsSatisfiedBy(Summary(OrderStatus.Draft, Channel.Web));

        spec.ExpressionRequests.Should().Be(1);
    }

    private static OrderSummary Summary(OrderStatus status, Channel channel)
        => new(OrderId.New(), status, channel, new Money(1m, s_eur));

    private sealed class OrderWithId(OrderId id) : Specification<IOrderSummary>
    {
        public override Expression<Func<IOrderSummary, bool>> ToExpression() => o => o.Id == id;
    }

    private sealed class TotalAbove(Money minimum) : Specification<IOrderSummary>
    {
        public override Expression<Func<IOrderSummary, bool>> ToExpression()
            => o => o.Total.Currency == minimum.Currency && o.Total.Amount > minimum.Amount;
    }

    private sealed class CountingSpecification : Specification<IOrderSummary>
    {
        public int ExpressionRequests { get; private set; }

        public override Expression<Func<IOrderSummary, bool>> ToExpression()
        {
            ExpressionRequests++;
            return o => o.Status == OrderStatus.Placed;
        }
    }

    // Lists every distinct parameter that the expression refers to.
    private sealed class ParameterCollector : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _parameters = [];

        public static HashSet<ParameterExpression> Collect(Expression expression)
        {
            var collector = new ParameterCollector();
            collector.Visit(expression);
            return collector._parameters;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            _parameters.Add(node);
            return node;
        }
    }
}
