namespace CafeOrders.Core.Domain;

/// <summary>
/// Замовлення гостя — агрегат: рядки й стан змінюються лише його методами за таблицею переходів.
/// </summary>
public sealed class Order
{
    private readonly List<OrderLine> _lines = new();

    /// <summary>Ініціалізує нове замовлення у стані <see cref="OrderStatus.New"/>.</summary>
    /// <param name="id">Номер замовлення, більший за нуль.</param>
    /// <param name="guestId">Ідентифікатор гостя, більший за нуль.</param>
    /// <param name="createdAt">Момент створення замовлення.</param>
    /// <exception cref="ArgumentOutOfRangeException">Номер або ідентифікатор гостя не додатні.</exception>
    public Order(int id, int guestId, DateTimeOffset createdAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(id);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(guestId);

        Id = id;
        GuestId = guestId;
        CreatedAt = createdAt;
        Status = OrderStatus.New;
    }

    /// <summary>Отримує номер замовлення.</summary>
    public int Id { get; }

    /// <summary>Отримує ідентифікатор гостя.</summary>
    public int GuestId { get; }

    /// <summary>Отримує момент створення замовлення.</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Отримує ознаку обслуговування за столиком (впливає на сервісний збір).</summary>
    public bool IsTableService { get; init; }

    /// <summary>Отримує поточний стан; змінюється лише методами-переходами.</summary>
    public OrderStatus Status { get; private set; }

    // Гість бачить рядки, але додати їх повз AddLine (і повз перевірку стану) не може.

    /// <summary>Отримує рядки замовлення лише для читання.</summary>
    public IReadOnlyList<OrderLine> Lines => _lines;

    /// <summary>Додає рядок до замовлення.</summary>
    /// <param name="line">Рядок замовлення.</param>
    /// <exception cref="InvalidOperationException">Замовлення вже не в стані New.</exception>
    public void AddLine(OrderLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        EnsureStatus(OrderStatus.New, "Рядки додають лише до нового замовлення.");

        _lines.Add(line);
    }

    /// <summary>Передає замовлення на кухню: New → Preparing.</summary>
    /// <exception cref="InvalidOperationException">Перехід заборонено або замовлення порожнє.</exception>
    public void StartPreparing()
    {
        EnsureStatus(OrderStatus.New, "На кухню передають лише нове замовлення.");
        if (_lines.Count == 0)
        {
            throw new InvalidOperationException("Порожнє замовлення не можна передати на кухню.");
        }

        Status = OrderStatus.Preparing;
    }

    /// <summary>Позначає, що страви подано гостю: Preparing → Served.</summary>
    /// <exception cref="InvalidOperationException">Замовлення не готується.</exception>
    public void Serve()
    {
        EnsureStatus(OrderStatus.Preparing, "Подати можна лише замовлення, що готується.");
        Status = OrderStatus.Served;
    }

    /// <summary>Закриває рахунок: Served → Paid.</summary>
    /// <exception cref="InvalidOperationException">Страви ще не подано.</exception>
    public void Pay()
    {
        EnsureStatus(OrderStatus.Served, "Оплатити можна лише подане замовлення.");
        Status = OrderStatus.Paid;
    }

    /// <summary>Скасовує замовлення зі стану New або Preparing.</summary>
    /// <exception cref="InvalidOperationException">Страви вже подано або замовлення закрите.</exception>
    public void Cancel()
    {
        if (Status is not (OrderStatus.New or OrderStatus.Preparing))
        {
            throw new InvalidOperationException($"Замовлення у стані {Status} скасувати не можна.");
        }

        Status = OrderStatus.Cancelled;
    }

    private void EnsureStatus(OrderStatus expected, string message)
    {
        if (Status != expected)
        {
            throw new InvalidOperationException($"{message} Поточний стан: {Status}.");
        }
    }
}
