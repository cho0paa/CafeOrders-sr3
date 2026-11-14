using System.Globalization;
using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Domain;
using CafeOrders.Core.Errors;
using CafeOrders.Core.Reporting;
using CafeOrders.Core.Services;

namespace CafeOrders.App.Ui;

/// <summary>Консольне меню каси: читає ввід касира, викликає сценарії CafeDesk і друкує результат.</summary>
internal sealed class CafeConsole
{
    private readonly CafeDesk _desk;
    private readonly IMenuCatalog _menu;
    private readonly TimeProvider _clock;
    private readonly ConsoleIo _io;

    /// <summary>Ініціалізує меню.</summary>
    /// <param name="desk">Сервіс сценаріїв.</param>
    /// <param name="menu">Довідник меню для показу.</param>
    /// <param name="clock">Годинник для моменту відкриття замовлення.</param>
    /// <param name="io">Ввід і вивід.</param>
    public CafeConsole(CafeDesk desk, IMenuCatalog menu, TimeProvider clock, ConsoleIo io)
    {
        _desk = desk;
        _menu = menu;
        _clock = clock;
        _io = io;
    }

    /// <summary>Цикл меню до команди «0» або кінця вводу.</summary>
    public void Run()
    {
        while (true)
        {
            _io.Write("\n1 відкрити  2 додати  3 на кухню  4 подано  5 рахунок  6 скасувати  7 пошук  8 популярність  9 меню  0 вихід\n> ");
            string? command = _io.ReadLine();
            if (command is null || command.Trim() == "0")
            {
                return;
            }

            try
            {
                Execute(command.Trim());
            }
            catch (DomainRuleException ex)
            {
                _io.Write($"Відхилено: {ex.Message}\n");
            }
        }
    }

    private void Execute(string command)
    {
        // Таблиця команд замість switch на 10 гілок (межа складності S, стандарт 5.2).
        var commands = new Dictionary<string, Action>
        {
            ["1"] = Open,
            ["2"] = Add,
            ["3"] = () => Step("Передано на кухню", _desk.SendToKitchen),
            ["4"] = () => Step("Страви подано", _desk.Serve),
            ["5"] = Pay,
            ["6"] = () => Step("Замовлення скасовано", _desk.Cancel),
            ["7"] = Search,
            ["8"] = () => _io.Write(PopularityReport.TextOf(_desk.Popularity())),
            ["9"] = ShowMenu,
        };
        if (commands.TryGetValue(command, out Action? action))
        {
            action();
        }
        else
        {
            _io.Write("Невідома команда\n");
        }
    }

    private void Step(string done, Action<int> scenario)
    {
        scenario(_io.ReadPositiveInt("Номер замовлення: "));
        _io.Write(done + "\n");
    }

    private void Open()
    {
        int guestId = _io.ReadPositiveInt("Номер гостя: ");
        bool table = _io.ReadYesNo("За столиком (так/ні): ");
        int id = _desk.OpenOrder(guestId, table, _clock.GetLocalNow());
        _io.Write($"Відкрито замовлення {id}\n");
    }

    private void Add()
    {
        int id = _io.ReadPositiveInt("Номер замовлення: ");
        string code = _io.ReadNonEmpty("Код позиції: ");
        int quantity = _io.ReadPositiveInt("Кількість: ");
        OrderLine line = _desk.AddItem(id, code, quantity);
        _io.Write(string.Create(CultureInfo.InvariantCulture, $"Додано: {line.MenuCode} x {line.Quantity} = {line.Amount:F2} грн\n"));
    }

    private void Pay()
    {
        int id = _io.ReadPositiveInt("Номер замовлення: ");
        BillSummary bill = _desk.BillOf(id);
        _io.Write(string.Create(CultureInfo.InvariantCulture, $"Страви {bill.Subtotal:F2}, сервісний збір {bill.ServiceCharge:F2}, разом {bill.Total:F2} грн\n"));
        decimal paid = _io.ReadMoney("Внесено: ");
        _desk.CloseBill(id, paid);
        _io.Write(string.Create(CultureInfo.InvariantCulture, $"Рахунок закрито, решта {paid - bill.Total:F2} грн\n"));
    }

    private void Search()
    {
        OrderStatus? status = _io.ReadOptionalStatus("Стан (New/Preparing/Served/Paid/Cancelled, порожньо — будь-який): ");
        int? guest = _io.ReadOptionalInt("Номер гостя (порожньо — будь-який): ");
        IReadOnlyList<Order> found = _desk.Search(status, guest, null);
        if (found.Count == 0)
        {
            _io.Write("Нічого не знайдено\n");
            return;
        }

        foreach (Order order in found)
        {
            _io.Write(string.Create(CultureInfo.InvariantCulture, $"№ {order.Id}: гість {order.GuestId}, {order.Status}, позицій {order.Lines.Count}, разом {_desk.BillOf(order.Id).Total:F2}\n"));
        }
    }

    private void ShowMenu()
    {
        foreach (MenuItem item in _menu.Items.OrderBy(i => i.Code, StringComparer.Ordinal))
        {
            string stop = item.IsAvailable ? string.Empty : " (стоп-лист)";
            _io.Write(string.Create(CultureInfo.InvariantCulture, $"{item.Code}  {item.Title}  {item.Price:F2}{stop}\n"));
        }

        foreach (ComboMeal combo in _menu.Combos.OrderBy(c => c.Code, StringComparer.Ordinal))
        {
            _io.Write(string.Create(CultureInfo.InvariantCulture, $"{combo.Code}  {combo.Title}  {combo.Price:F2}\n"));
        }
    }
}
