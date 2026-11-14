using CafeOrders.App.Infrastructure;
using CafeOrders.App.Ui;
using CafeOrders.Core.Domain;

namespace CafeOrders.Tests;

// Архітектурне правило залежностей і інфраструктура застосунку (файли — у тимчасовій теці).
public class ArchitectureAndInfrastructureTests
{
    [Fact]
    public void Core_DoesNotReferenceAppOrConsoleLogging()
    {
        var references = typeof(Order).Assembly.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(references, n => n.StartsWith("CafeOrders.App", StringComparison.Ordinal));
        Assert.DoesNotContain(references, n => n.Contains("Logging.Console", StringComparison.Ordinal));
    }

    [Fact]
    public void JsonOrderRepository_SaveAndReload_RestoresStatusAndLines()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cafe-orders-{Guid.NewGuid():N}.json");
        try
        {
            var repository = new JsonOrderRepository(path);
            var order = new Order(5, 7, DateTimeOffset.UnixEpoch) { IsTableService = true };
            order.AddLine(new OrderLine("CAP-001", 2, 65m));
            order.StartPreparing();
            order.Serve();
            repository.Add(order);
            repository.Save();

            Order restored = new JsonOrderRepository(path).GetById(5)!;

            Assert.Equal(OrderStatus.Served, restored.Status);
            Assert.True(restored.IsTableService);
            Assert.Equal(order.Lines, restored.Lines);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void JsonMenuCatalog_ProjectMenu_HasAtLeastFiftyItemsAndStopList()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "data", "menu.json");

        var menu = JsonMenuCatalog.Load(path);

        Assert.True(menu.Items.Count >= 50);
        Assert.False(menu.FindItem("tea-005")!.IsAvailable);
        Assert.NotNull(menu.FindCombo("CMB-002"));
    }

    [Fact]
    public void ConsoleIo_InvalidThenValidNumber_AsksAgain()
    {
        using var output = new StringWriter();
        var io = new ConsoleIo(new StringReader("дві\n-1\n3\n"), output, echo: false);

        int value = io.ReadPositiveInt("Кількість: ");

        Assert.Equal(3, value);
        Assert.Contains("Помилка вводу", output.ToString(), StringComparison.Ordinal);
    }
}
