using CafeOrders.Core.Domain;
using CafeOrders.Core.Pricing;
using CafeOrders.Core.Reporting;
using CafeOrders.Core.Services;

namespace CafeOrders.Tests;

// Таблиця рішень профільного правила (docs/spec.md, розділ 1.4): кожен рядок — один випадок.
public class BillCalculatorTests
{
    [Theory]
    [InlineData(400, false, 0, 400)]
    [InlineData(400, true, 20, 420)]
    [InlineData(999.99, false, 0, 999.99)]
    [InlineData(999.99, true, 50, 1049.99)]
    [InlineData(1000, false, 100, 1100)]
    [InlineData(1000, true, 150, 1150)]
    [InlineData(5000, true, 150, 5150)]
    [InlineData(5000, false, 150, 5150)]
    public void BillOf_DecisionTable_MatchesSpec(double subtotal, bool table, double charge, double total)
    {
        Order order = OrderWith(table, (decimal)subtotal);

        BillSummary bill = new BillCalculator(new ServiceChargePolicy()).BillOf(order);

        Assert.Equal(new BillSummary((decimal)subtotal, (decimal)charge, (decimal)total), bill);
    }

    [Fact]
    public void BillOf_EmptyOrder_ReturnsZeros()
    {
        Order order = new(1, 1, DateTimeOffset.UnixEpoch) { IsTableService = true };

        BillSummary bill = new BillCalculator(new ServiceChargePolicy()).BillOf(order);

        Assert.Equal(new BillSummary(0m, 0m, 0m), bill);
    }

    [Fact]
    public void PopularityReport_NoPaidOrders_PrintsNotice()
    {
        string text = PopularityReport.TextOf(PopularityReport.Build(new List<Order>()));

        Assert.Equal("Оплачених замовлень немає." + Environment.NewLine, text);
    }

    [Fact]
    public void PopularityReport_SeveralOrders_SortsByPortionsThenCode()
    {
        Order first = PaidOrder(1, ("CRO-001", 1, 55m), ("CAP-001", 3, 65m));
        Order second = PaidOrder(2, ("DES-002", 3, 125m), ("CAP-001", 1, 65m));

        IReadOnlyList<PopularityRow> rows = PopularityReport.Build(new[] { first, second });

        Assert.Equal(new[] { "CAP-001", "DES-002", "CRO-001" }, rows.Select(r => r.MenuCode));
        Assert.Equal(new PopularityRow("CAP-001", 4, 260m), rows[0]);
    }

    [Fact]
    public void PopularityReport_TextOf_PrintsAlignedTable()
    {
        var rows = new List<PopularityRow> { new("CAP-001", 4, 260m) };

        string text = PopularityReport.TextOf(rows).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal("Код         Порцій      Виторг\nCAP-001          4      260.00\n", text);
    }

    [Fact]
    public void ComboMeal_SingleComponent_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ComboMeal("CMB-9", "Один", new[] { "CAP-001" }, 50m));
    }

    [Fact]
    public void ComboMeal_SourceListChanged_KeepsOriginalComposition()
    {
        var components = new List<string> { "CAP-001", "CRO-001" };
        var combo = new ComboMeal("CMB-1", "Сніданок", components, 105m);

        components.Add("DES-002");

        Assert.Equal(2, combo.Components.Count);
    }

    private static Order OrderWith(bool table, decimal subtotal)
    {
        var order = new Order(1, 1, DateTimeOffset.UnixEpoch) { IsTableService = table };
        order.AddLine(new OrderLine("SET-001", 1, subtotal));
        return order;
    }

    private static Order PaidOrder(int id, params (string Code, int Qty, decimal Price)[] lines)
    {
        var order = new Order(id, 1, DateTimeOffset.UnixEpoch);
        foreach (var l in lines)
        {
            order.AddLine(new OrderLine(l.Code, l.Qty, l.Price));
        }

        order.StartPreparing();
        order.Serve();
        order.Pay();
        return order;
    }
}
