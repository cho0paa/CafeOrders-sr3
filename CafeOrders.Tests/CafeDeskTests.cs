using CafeOrders.Core.Domain;
using CafeOrders.Core.Errors;
using CafeOrders.Core.Pricing;
using CafeOrders.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeOrders.Tests;

// Сценарії прикладного шару СР3 (UC-01…UC-07) на фейковому сховищі й фейковому меню.
public class CafeDeskTests
{
    private static readonly DateTimeOffset Morning = new(2026, 11, 14, 9, 30, 0, TimeSpan.FromHours(2));

    [Fact]
    public void OpenOrder_EmptyRepository_GetsNumberOne()
    {
        CafeDesk desk = NewDesk(out _);

        int id = desk.OpenOrder(7, true, Morning);

        Assert.Equal(1, id);
    }

    [Fact]
    public void AddItem_MenuItemAndCombo_UsesMenuPrices()
    {
        CafeDesk desk = NewDesk(out _);
        int id = desk.OpenOrder(7, true, Morning);

        OrderLine coffee = desk.AddItem(id, "cap-001", 2);
        OrderLine combo = desk.AddItem(id, "CMB-001", 1);

        Assert.Equal(new OrderLine("CAP-001", 2, 65m), coffee);
        Assert.Equal(105m, combo.UnitPrice);
    }

    [Theory]
    [InlineData("TEA-005", "menu.stop-list")]
    [InlineData("XYZ-999", "menu.unknown")]
    public void AddItem_UnavailableOrUnknownCode_ThrowsMenuRule(string code, string rule)
    {
        CafeDesk desk = NewDesk(out _);
        int id = desk.OpenOrder(7, true, Morning);

        var ex = Assert.Throws<DomainRuleException>(() => desk.AddItem(id, code, 1));

        Assert.Equal(rule, ex.Rule);
    }

    [Fact]
    public void FullScenario_TableOrder_ClosesBillWithServiceCharge()
    {
        CafeDesk desk = NewDesk(out _);
        int id = desk.OpenOrder(7, true, Morning);
        desk.AddItem(id, "CAP-001", 2);
        desk.AddItem(id, "CMB-001", 2);
        desk.SendToKitchen(id);
        desk.Serve(id);

        BillSummary bill = desk.CloseBill(id, 400m);

        Assert.Equal(new BillSummary(340m, 17m, 357m), bill);
        Assert.Equal(OrderStatus.Paid, desk.Search(null, 7, null).Single().Status);
    }

    [Fact]
    public void CloseBill_NotServed_ThrowsPayableRule()
    {
        CafeDesk desk = NewDesk(out _);
        int id = desk.OpenOrder(7, false, Morning);
        desk.AddItem(id, "CAP-001", 1);

        var ex = Assert.Throws<DomainRuleException>(() => desk.CloseBill(id, 100m));

        Assert.Equal("order.payable", ex.Rule);
    }

    [Fact]
    public void CloseBill_NotEnoughMoney_ThrowsAmountRuleAndKeepsServed()
    {
        CafeDesk desk = NewDesk(out _);
        int id = ServedOrder(desk);

        var ex = Assert.Throws<DomainRuleException>(() => desk.CloseBill(id, 10m));

        Assert.Equal("order.amount", ex.Rule);
        Assert.Equal(OrderStatus.Served, desk.Search(OrderStatus.Served, null, null).Single().Status);
    }

    [Fact]
    public void Cancel_ServedOrder_ThrowsCancelRule()
    {
        CafeDesk desk = NewDesk(out _);
        int id = ServedOrder(desk);

        var ex = Assert.Throws<DomainRuleException>(() => desk.Cancel(id));

        Assert.Equal("order.cancel", ex.Rule);
    }

    [Fact]
    public void AddItem_AfterKitchen_ThrowsEditableRule()
    {
        CafeDesk desk = NewDesk(out _);
        int id = desk.OpenOrder(7, true, Morning);
        desk.AddItem(id, "CAP-001", 1);
        desk.SendToKitchen(id);

        var ex = Assert.Throws<DomainRuleException>(() => desk.AddItem(id, "CRO-001", 1));

        Assert.Equal("order.editable", ex.Rule);
    }

    [Fact]
    public void Serve_UnknownOrder_ThrowsExistsRule()
    {
        CafeDesk desk = NewDesk(out _);

        var ex = Assert.Throws<DomainRuleException>(() => desk.Serve(42));

        Assert.Equal("order.exists", ex.Rule);
    }

    [Fact]
    public void Search_NoMatches_ReturnsEmpty()
    {
        CafeDesk desk = NewDesk(out _);
        desk.OpenOrder(7, true, Morning);

        Assert.Empty(desk.Search(OrderStatus.Paid, null, null));
    }

    [Fact]
    public void Search_ByGuestAndDay_ReturnsOrdersInIdOrder()
    {
        CafeDesk desk = NewDesk(out _);
        int first = desk.OpenOrder(7, true, Morning);
        desk.OpenOrder(8, true, Morning);
        int third = desk.OpenOrder(7, false, Morning);
        desk.OpenOrder(7, false, Morning.AddDays(1));

        IReadOnlyList<Order> found = desk.Search(null, 7, new DateOnly(2026, 11, 14));

        Assert.Equal(new[] { first, third }, found.Select(o => o.Id));
    }

    [Fact]
    public void Popularity_PaidAndCancelledOrders_CountsOnlyPaid()
    {
        CafeDesk desk = NewDesk(out FakeOrderRepository repository);
        int paid = ServedOrder(desk);
        desk.CloseBill(paid, 1000m);
        int cancelled = desk.OpenOrder(8, true, Morning);
        desk.AddItem(cancelled, "DES-002", 5);
        desk.Cancel(cancelled);

        var rows = desk.Popularity();

        Assert.Equal("CAP-001", Assert.Single(rows).MenuCode);
        Assert.True(repository.AddCalls >= 2);
    }

    private static int ServedOrder(CafeDesk desk)
    {
        int id = desk.OpenOrder(7, true, Morning);
        desk.AddItem(id, "CAP-001", 2);
        desk.SendToKitchen(id);
        desk.Serve(id);
        return id;
    }

    private static CafeDesk NewDesk(out FakeOrderRepository repository)
    {
        repository = new FakeOrderRepository();
        return new CafeDesk(
            repository,
            new FakeMenuCatalog(),
            new BillCalculator(new ServiceChargePolicy()),
            NullLogger<CafeDesk>.Instance);
    }
}
