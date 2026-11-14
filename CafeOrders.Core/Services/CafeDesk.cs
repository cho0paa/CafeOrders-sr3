using System.Globalization;
using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Domain;
using CafeOrders.Core.Errors;
using CafeOrders.Core.Reporting;
using Microsoft.Extensions.Logging;

namespace CafeOrders.Core.Services;

/// <summary>
/// Прикладний сервіс каси кав’ярні: сценарії UC-01…UC-07. Ввід уже перевірено барикадою застосунку.
/// </summary>
public sealed class CafeDesk
{
    private readonly IOrderRepository _orders;
    private readonly IMenuCatalog _menu;
    private readonly BillCalculator _bills;
    private readonly ILogger<CafeDesk> _log;

    /// <summary>Ініціалізує сервіс залежностями; конкретні класи обирає композиційний корінь.</summary>
    /// <param name="orders">Сховище замовлень.</param>
    /// <param name="menu">Довідник меню.</param>
    /// <param name="bills">Розрахунок рахунку.</param>
    /// <param name="log">Журнал сервісу.</param>
    public CafeDesk(IOrderRepository orders, IMenuCatalog menu, BillCalculator bills, ILogger<CafeDesk> log)
    {
        _orders = orders ?? throw new ArgumentNullException(nameof(orders));
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _bills = bills ?? throw new ArgumentNullException(nameof(bills));
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    /// <summary>UC-01: відкриває нове замовлення.</summary>
    /// <param name="guestId">Ідентифікатор гостя, більший за нуль.</param>
    /// <param name="isTableService">Чи обслуговують гостя за столиком.</param>
    /// <param name="now">Момент відкриття (з годинника застосунку).</param>
    /// <returns>Номер нового замовлення.</returns>
    public int OpenOrder(int guestId, bool isTableService, DateTimeOffset now)
    {
        IReadOnlyList<Order> all = _orders.GetAll();
        int id = all.Count == 0 ? 1 : all.Max(o => o.Id) + 1;
        var order = new Order(id, guestId, now) { IsTableService = isTableService };
        _orders.Add(order);
        _orders.Save();
        _log.LogInformation("UC-01: відкрито замовлення {OrderId}, гість {GuestId}", id, guestId);
        return id;
    }

    /// <summary>UC-02: додає позицію меню або комплекс до нового замовлення.</summary>
    /// <param name="orderId">Номер замовлення.</param>
    /// <param name="code">Код позиції меню або комплексу.</param>
    /// <param name="quantity">Кількість порцій, більша за нуль.</param>
    /// <returns>Доданий рядок із ціною на момент замовлення.</returns>
    /// <exception cref="DomainRuleException">Замовлення немає, воно не нове, код невідомий або в стоп-листі.</exception>
    public OrderLine AddItem(int orderId, string code, int quantity)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        using IDisposable? scope = _log.BeginScope("UC-02 Order:{OrderId}", orderId);
        Order order = Require(orderId);
        EnsureStatus(order, OrderStatus.New, "order.editable", "Додавати позиції можна лише до нового замовлення");
        var line = new OrderLine(code.Trim().ToUpperInvariant(), quantity, PriceOf(code));
        order.AddLine(line);
        _orders.Save();
        _log.LogInformation("Додано {MenuCode} x {Quantity}", line.MenuCode, line.Quantity);
        return line;
    }

    /// <summary>UC-03: передає замовлення на кухню (New → Preparing).</summary>
    /// <param name="orderId">Номер замовлення.</param>
    public void SendToKitchen(int orderId) =>
        Transition(orderId, OrderStatus.New, "order.kitchen", "На кухню передають лише нове замовлення", o => o.StartPreparing());

    /// <summary>UC-03: позначає, що страви подано (Preparing → Served).</summary>
    /// <param name="orderId">Номер замовлення.</param>
    public void Serve(int orderId) =>
        Transition(orderId, OrderStatus.Preparing, "order.serve", "Подати можна лише замовлення, що готується", o => o.Serve());

    /// <summary>UC-04: розраховує рахунок без зміни стану.</summary>
    /// <param name="orderId">Номер замовлення.</param>
    /// <returns>Рахунок із сервісним збором.</returns>
    public BillSummary BillOf(int orderId) => _bills.BillOf(Require(orderId));

    /// <summary>UC-04: закриває рахунок (Served → Paid).</summary>
    /// <param name="orderId">Номер замовлення.</param>
    /// <param name="paid">Внесена сума, грн.</param>
    /// <returns>Рахунок, за яким сплачено.</returns>
    /// <exception cref="DomainRuleException">Стан не дозволяє оплату або внесено менше за підсумок.</exception>
    public BillSummary CloseBill(int orderId, decimal paid)
    {
        using IDisposable? scope = _log.BeginScope("UC-04 Order:{OrderId}", orderId);
        Order order = Require(orderId);
        EnsureStatus(order, OrderStatus.Served, "order.payable", "Закрити можна лише поданий рахунок");
        BillSummary bill = _bills.BillOf(order);
        if (paid < bill.Total)
        {
            _log.LogWarning("Оплату відхилено: внесено {Paid}, треба {Total}", paid, bill.Total);
            throw new DomainRuleException(
                "order.amount",
                string.Create(CultureInfo.InvariantCulture, $"Внесено {paid:F2}, треба {bill.Total:F2}"));
        }

        order.Pay();
        _orders.Save();
        _log.LogInformation("Рахунок закрито на {Total}", bill.Total);
        return bill;
    }

    /// <summary>UC-05: скасовує замовлення до подачі страв.</summary>
    /// <param name="orderId">Номер замовлення.</param>
    /// <exception cref="DomainRuleException">Страви вже подано або замовлення закрите.</exception>
    public void Cancel(int orderId)
    {
        Order order = Require(orderId);
        if (order.Status is not (OrderStatus.New or OrderStatus.Preparing))
        {
            _log.LogWarning("Скасування відхилено: замовлення {OrderId} у стані {Status}", orderId, order.Status);
            throw new DomainRuleException("order.cancel", $"Замовлення у стані {order.Status} скасувати не можна");
        }

        order.Cancel();
        _orders.Save();
        _log.LogInformation("UC-05: скасовано замовлення {OrderId}", orderId);
    }

    /// <summary>UC-06: шукає замовлення за станом, гостем і датою; null у критерії — «будь-який».</summary>
    /// <param name="status">Стан замовлення.</param>
    /// <param name="guestId">Ідентифікатор гостя.</param>
    /// <param name="day">Дата відкриття.</param>
    /// <returns>Замовлення за зростанням номера; порожній список, якщо нічого не знайдено.</returns>
    public IReadOnlyList<Order> Search(OrderStatus? status, int? guestId, DateOnly? day) =>
        _orders.GetAll()
            .Where(o => status is null || o.Status == status)
            .Where(o => guestId is null || o.GuestId == guestId)
            .Where(o => day is null || DateOnly.FromDateTime(o.CreatedAt.Date) == day)
            .OrderBy(o => o.Id)
            .ToList();

    /// <summary>UC-07: звіт «популярність позицій» за оплаченими замовленнями.</summary>
    /// <returns>Рядки звіту за спаданням порцій.</returns>
    public IReadOnlyList<PopularityRow> Popularity() => PopularityReport.Build(_orders.GetAll());

    private static void EnsureStatus(Order order, OrderStatus expected, string rule, string message)
    {
        if (order.Status != expected)
        {
            throw new DomainRuleException(rule, $"{message}; поточний стан {order.Status}");
        }
    }

    private decimal PriceOf(string code)
    {
        MenuItem? item = _menu.FindItem(code);
        if (item is not null)
        {
            if (!item.IsAvailable)
            {
                _log.LogWarning("Позиція {MenuCode} у стоп-листі", item.Code);
                throw new DomainRuleException("menu.stop-list", $"{item.Title} зараз недоступна");
            }

            return item.Price;
        }

        ComboMeal? combo = _menu.FindCombo(code);
        if (combo is null)
        {
            _log.LogWarning("Невідомий код позиції {MenuCode}", code);
            throw new DomainRuleException("menu.unknown", $"Позиції {code} немає в меню");
        }

        return combo.Price;
    }

    private void Transition(int orderId, OrderStatus expected, string rule, string message, Action<Order> step)
    {
        Order order = Require(orderId);
        if (order.Status != expected)
        {
            _log.LogWarning("Перехід відхилено: замовлення {OrderId} у стані {Status}", orderId, order.Status);
            throw new DomainRuleException(rule, $"{message}; поточний стан {order.Status}");
        }

        step(order);
        _orders.Save();
        _log.LogInformation("Замовлення {OrderId} → {Status}", orderId, order.Status);
    }

    private Order Require(int orderId)
    {
        Order? order = _orders.GetById(orderId);
        if (order is null)
        {
            _log.LogWarning("Замовлення {OrderId} не знайдено", orderId);
            throw new DomainRuleException("order.exists", $"Замовлення {orderId} не знайдено");
        }

        return order;
    }
}
