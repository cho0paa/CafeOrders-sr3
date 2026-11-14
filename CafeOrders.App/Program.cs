using System.Diagnostics.CodeAnalysis;
using System.Text;
using CafeOrders.App;
using CafeOrders.App.Infrastructure;
using CafeOrders.App.Input;
using CafeOrders.App.Ui;
using CafeOrders.Core;
using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Billing;
using CafeOrders.Core.Documents;
using CafeOrders.Core.Domain;
using CafeOrders.Core.Errors;
using CafeOrders.Core.Pricing;
using CafeOrders.Core.Reporting;
using CafeOrders.Core.Services;
using CafeOrders.Core.Storage;
using Microsoft.Extensions.Logging;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;
Console.WriteLine($"CafeOrders v{VersionInfo.Current()}");

using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "HH:mm:ss ";
        options.IncludeScopes = true;
    });
    builder.SetMinimumLevel(LogLevel.Debug);
});

return RunWithTopLevelHandler(args, loggerFactory);

// Верхній обробник — єдине місце, де дозволено перехоплювати будь-який виняток.
[SuppressMessage(
    "Design",
    "CA1031:Do not catch general exception types",
    Justification = "Верхній обробник застосунку: записує збій у журнал і повертає код виходу")]
static int RunWithTopLevelHandler(string[] args, ILoggerFactory loggerFactory)
{
    ILogger log = loggerFactory.CreateLogger("CafeOrders.App");
    try
    {
        Run(args, loggerFactory);
        return 0;
    }
    catch (DomainRuleException ex)
    {
        log.LogWarning("Правило {Rule}: {Message}", ex.Rule, ex.Message);
        return 1;
    }
    catch (Exception ex)
    {
        log.LogCritical(ex, "Непередбачений збій");
        return 2;
    }
}

static void Run(string[] args, ILoggerFactory loggerFactory)
{
    // Режими запуску: без аргументу — каса (СР3); решта — демонстрації лабораторних робіт.
    var modes = new Dictionary<string, Action>(StringComparer.Ordinal)
    {
        ["add-line"] = () => RunAddLineDialog(loggerFactory),
        ["logging"] = () => RunLoggingDemo(loggerFactory),
        ["demo"] = () => RunDemos(loggerFactory),
    };
    string mode = args.Length > 0 ? args[0] : string.Empty;
    if (modes.TryGetValue(mode, out Action? run))
    {
        run();
    }
    else
    {
        RunCafe();
    }
}

static void RunDemos(ILoggerFactory loggerFactory)
{
    RunBillDemo();
    Console.WriteLine();
    RunCalculationDemo();
    Console.WriteLine();
    RunServiceDemo(loggerFactory);
}

// СР3: каса кав’ярні — композиційний корінь основного режиму.
static void RunCafe()
{
    Directory.CreateDirectory("data");
    using var fileLog = new FileLoggerProvider(Path.Combine("data", "cafe.local.log"));
    using ILoggerFactory factory = LoggerFactory.Create(builder => builder.AddProvider(fileLog));
    var menu = JsonMenuCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "menu.json"));
    var orders = new JsonOrderRepository(Path.Combine("data", "orders.local.json"));
    var desk = new CafeDesk(orders, menu, new BillCalculator(new ServiceChargePolicy()), factory.CreateLogger<CafeDesk>());
    var io = new ConsoleIo(Console.In, Console.Out, Console.IsInputRedirected);
    Console.WriteLine($"Каса кав’ярні: меню {menu.Items.Count} позицій і {menu.Combos.Count} комплекси, замовлень {orders.GetAll().Count}");
    new CafeConsole(desk, menu, TimeProvider.System, io).Run();
}

// ЛР2: модуль рахунку, приведений до стандарту (вивід звіряється з docs/lab02/before.txt).
static void RunBillDemo()
{
    var bill = new Bill("A-1001", "Іваненко");
    bill.AddLine("ESP-1", 3, 250m);
    bill.AddLine("CRS-2", 12, 90m);

    Console.WriteLine(bill.CalculateTotal(false));
    Console.WriteLine(bill.CalculateTotal(true));
    Console.WriteLine(bill.IsValid());
    Console.WriteLine(bill.TryChangeStatus(1));
    Console.Write(bill.BuildReport());
}

// ЛР3: розрахунок підсумку запитом і друк чека окремою командою.
static void RunCalculationDemo()
{
    TotalRequest request = BuildSampleRequest();
    TotalResult result = DocumentTotalCalculator.Calculate(request);
    string report = DocumentReport.TextOf(request, result);
    new DocumentReportPrinter(Console.Out).Print(report);
}

// ЛР4: композиційний корінь — єдине місце, де створюються конкретні реалізації.
static void RunServiceDemo(ILoggerFactory loggerFactory)
{
    // Таблиця відповідності замість ланцюга if у логіці: новий тип гостя — новий рядок тут.
    var policies = new Dictionary<string, IPricingPolicy>
    {
        ["regular"] = new DiscountPricingPolicy(0.05m),
        ["vip"] = new VipPricingPolicy(),
        ["staff"] = new DiscountPricingPolicy(0.30m),
    };
    const string guestType = "regular";
    IPricingPolicy pricing = policies.TryGetValue(guestType, out IPricingPolicy? found)
        ? found
        : new RegularPricingPolicy();

    var repository = new InMemoryOrderRepository();
    OrderService service = CreateService(repository, pricing, loggerFactory);

    var order = new Order(1, 100, DateTimeOffset.Now);
    order.AddLine(new OrderLine("CAP-001", 2, 250m));
    service.Place(order);

    Console.WriteLine($"Сума: {service.TotalOf(order):0.00}");
    Console.Write(OrderCsvReport.BuildCsv(repository.GetAll(), service.TotalOf));
}

// ЛР5: введення рядків замовлення касиром; помилка вводу — повідомлення й повторний запит.
static void RunAddLineDialog(ILoggerFactory loggerFactory)
{
    var repository = new InMemoryOrderRepository();
    OrderService service = CreateService(repository, new RegularPricingPolicy(), loggerFactory);
    var order = new Order(1, 100, DateTimeOffset.Now);
    service.Place(order);

    while (true)
    {
        Console.Write("Код позиції (порожній рядок — завершити): ");
        string? menuCode = ReadInput();
        if (string.IsNullOrWhiteSpace(menuCode))
        {
            break;
        }

        Console.Write("Кількість: ");
        string? quantity = ReadInput();
        Console.Write("Ціна: ");
        string? price = ReadInput();

        Result<OrderLine> parsed = OrderLineParser.Parse(menuCode, quantity, price);
        if (!parsed.IsSuccess)
        {
            Console.WriteLine($"Помилка вводу: {parsed.Error}");
            continue;
        }

        service.AddLine(order.Id, parsed.Value!);
        Console.WriteLine($"Додано: {parsed.Value!.MenuCode} x {parsed.Value.Quantity}");
    }

    Console.WriteLine($"Разом: {service.TotalOf(order):0.00}");
}

// ЛР5: сценарій із записами всіх рівнів журналу, аудитом у файл і відхиленою операцією.
static void RunLoggingDemo(ILoggerFactory loggerFactory)
{
    ILogger log = loggerFactory.CreateLogger("CafeOrders.App");
    var repository = new InMemoryOrderRepository();
    OrderService service = CreateService(repository, new RegularPricingPolicy(), loggerFactory);

    Directory.CreateDirectory("data");
    const string auditPath = "data/audit.local.log";
    using var audit = new FileAuditLog(auditPath);

    var order = new Order(1042, 100, DateTimeOffset.Now);
    order.AddLine(new OrderLine("CAP-001", 2, 55.50m));
    service.Place(order);
    service.AddLine(order.Id, new OrderLine("CAK-002", 1, 78.50m));
    audit.Write("place", order.Id);
    order.StartPreparing();
    order.Serve();
    service.Pay(order.Id, 200m);
    audit.Write("pay", order.Id);

    // Другий екземпляр журналу на той самий файл: файл зайнятий — помилка середовища (errors.md, № 10).
    try
    {
        using var second = new FileAuditLog(auditPath);
    }
    catch (IOException ex)
    {
        log.LogError(ex, "Журнал аудиту {Path} заблоковано", auditPath);
    }

    string[] typedQuantities = { "2", "дві", "-1", "5" };
    int valid = OrderLineParser.CountValidQuantities(typedQuantities);
    Console.WriteLine($"Коректних кількостей: {valid}");
    new ReportExporter(service).ExportReport("data/report.local.txt", order);

    // Оплата неіснуючого замовлення: правило order.exists, обробляє верхній обробник.
    service.Pay(7, 100m);
}

static OrderService CreateService(
    IOrderRepository repository, IPricingPolicy pricing, ILoggerFactory loggerFactory) =>
    new(repository, pricing, new ConsoleNotifier(), loggerFactory.CreateLogger<OrderService>());

// Коли ввід перенаправлено з файлу, повторюємо його у виводі, щоб журнал сеансу був читабельним.
static string? ReadInput()
{
    string? value = Console.ReadLine();
    if (Console.IsInputRedirected)
    {
        Console.WriteLine(value);
    }

    return value;
}

// Замовлення для демонстрації розрахунку ЛР3 задано в коді.
static TotalRequest BuildSampleRequest()
{
    return new TotalRequest(
        DocumentId: 1,
        Client: new ClientInfo("Іваненко", "i@ex.com", true),
        Lines: new List<OrderLine>
        {
            new("CAP-001", 12, 100m),
            new("CAK-002", 1, 250m),
        },
        CreatedAt: new DateOnly(2026, 3, 10),
        CouponCode: null,
        Status: OrderStatus.New,
        DeliveryPrice: 60m);
}
