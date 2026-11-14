using CafeOrders.App.Ui;
using CafeOrders.Core.Domain;
using CafeOrders.Core.Pricing;
using CafeOrders.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CafeOrders.Tests;

// Консольне меню на фейковому сховищі, фейковому меню і фіксованому годиннику.
public class CafeConsoleTests
{
    private static readonly DateTimeOffset Fixed = new(2026, 11, 14, 9, 30, 0, TimeSpan.FromHours(2));

    [Fact]
    public void Run_OpenCommand_UsesFixedClockForOrderDate()
    {
        CafeDesk desk = NewDesk();

        string output = RunConsole(desk, "1\n7\nтак\n0\n");

        Order order = Assert.Single(desk.Search(null, 7, new DateOnly(2026, 11, 14)));
        Assert.Equal(Fixed, order.CreatedAt);
        Assert.Contains("Відкрито замовлення 1", output, StringComparison.Ordinal);
    }

    [Fact]
    public void Run_UnknownMenuCode_PrintsRejectionAndContinues()
    {
        CafeDesk desk = NewDesk();

        string output = RunConsole(desk, "1\n7\nні\n2\n1\nXYZ-999\n1\n0\n");

        Assert.Contains("Відхилено: Позиції XYZ-999 немає в меню", output, StringComparison.Ordinal);
        Assert.Empty(desk.Search(null, 7, null).Single().Lines);
    }

    private static string RunConsole(CafeDesk desk, string input)
    {
        using var output = new StringWriter();
        var io = new ConsoleIo(new StringReader(input), output, echo: false);
        new CafeConsole(desk, new FakeMenuCatalog(), new FixedClock(Fixed), io).Run();
        return output.ToString();
    }

    private static CafeDesk NewDesk() =>
        new(
            new FakeOrderRepository(),
            new FakeMenuCatalog(),
            new BillCalculator(new ServiceChargePolicy()),
            NullLogger<CafeDesk>.Instance);

    // Фіксований годинник: завжди повертає один і той самий момент.
    private sealed class FixedClock : TimeProvider
    {
        private readonly DateTimeOffset _now;

        public FixedClock(DateTimeOffset now) => _now = now;

        public override TimeZoneInfo LocalTimeZone =>
            TimeZoneInfo.CreateCustomTimeZone("UTC+2", TimeSpan.FromHours(2), "UTC+2", "UTC+2");

        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();
    }
}
