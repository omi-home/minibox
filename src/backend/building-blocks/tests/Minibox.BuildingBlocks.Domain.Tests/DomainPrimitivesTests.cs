namespace Minibox.BuildingBlocks.Domain.Tests;

public class DomainPrimitivesTests
{
    sealed record OrderPlaced(Guid OrderId) : IDomainEvent;

    sealed class Order(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Place() => Raise(new OrderPlaced(Id));
    }

    sealed class Line(int id) : Entity<int>(id);

    sealed class OtherLine(int id) : Entity<int>(id);

    [Fact(DisplayName = "Entity bằng nhau theo kiểu + Id")]
    public void entity_equality()
    {
        Assert.Equal(new Line(1), new Line(1));
        Assert.NotEqual(new Line(1), new Line(2));
        Assert.False(new Line(1).Equals(new OtherLine(1)));
        Assert.True(new Line(1) == new Line(1));
        Assert.Equal(new Line(1).GetHashCode(), new Line(1).GetHashCode());
    }

    [Fact(DisplayName = "AggregateRoot gom domain event, ClearDomainEvents xoá hết")]
    public void aggregate_collects_events()
    {
        var order = new Order(Guid.NewGuid());
        order.Place();
        Assert.Equal([new OrderPlaced(order.Id)], order.DomainEvents);
        order.ClearDomainEvents();
        Assert.Empty(order.DomainEvents);
    }

    [Fact(DisplayName = "Result: thành công mang giá trị, thất bại mang Error; đọc Value khi thất bại ném lỗi")]
    public void result()
    {
        Result<int> ok = 5;
        Result<int> fail = Error.Validation("qty.invalid", "Số lượng phải > 0");
        Assert.True(ok.IsSuccess);
        Assert.Equal(5, ok.Value);
        Assert.True(fail.IsFailure);
        Assert.Equal(ErrorType.Validation, fail.Error.Type);
        Assert.Throws<InvalidOperationException>(() => fail.Value);
        Assert.Throws<InvalidOperationException>(() => ok.Error);
        Assert.True(Result.Success().IsSuccess);
        Assert.Equal("x.nf", Result.Failure(Error.NotFound("x.nf", "?")).Error.Code);
    }

    [Fact(DisplayName = "Quantity: cộng cùng đơn vị, khác đơn vị ném lỗi, không âm, bằng nhau theo giá trị")]
    public void quantity()
    {
        Assert.Equal(new Quantity(3.5m, "kg"), new Quantity(1.5m, "kg") + new Quantity(2m, "kg"));
        Assert.Equal(new Quantity(1m, "kg"), new Quantity(3m, "kg") - new Quantity(2m, "kg"));
        Assert.Throws<InvalidOperationException>(() => new Quantity(1, "kg") + new Quantity(1, "cái"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(-1, "kg"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Quantity(1, "kg") - new Quantity(2, "kg"));
        Assert.Throws<ArgumentException>(() => new Quantity(1, " "));
        Assert.Equal(new Quantity(2.0m, "kg"), new Quantity(2m, "kg"));
    }
}
