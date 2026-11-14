using System.Text.Json;
using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Domain;

namespace CafeOrders.App.Infrastructure;

/// <summary>
/// Сховище замовлень у файлі JSON. Домен не знає про серіалізацію: зберігаються знімки, а стан
/// відновлюється повторним проходом дозволених переходів агрегату.
/// </summary>
internal sealed class JsonOrderRepository : IOrderRepository
{
    private static readonly JsonSerializerOptions s_options = new() { WriteIndented = true };

    private readonly string _path;
    private readonly Dictionary<int, Order> _orders = new();

    /// <summary>Відкриває сховище; якщо файлу немає — починає з порожнього.</summary>
    /// <param name="path">Шлях до файлу замовлень.</param>
    public JsonOrderRepository(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
        if (File.Exists(path))
        {
            foreach (OrderSnapshot snapshot in JsonSerializer.Deserialize<List<OrderSnapshot>>(File.ReadAllText(path)) ?? new())
            {
                Order order = snapshot.Restore();
                _orders[order.Id] = order;
            }
        }
    }

    /// <inheritdoc/>
    public void Add(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        if (!_orders.TryAdd(order.Id, order))
        {
            throw new InvalidOperationException($"Замовлення № {order.Id} уже існує.");
        }
    }

    /// <inheritdoc/>
    public Order? GetById(int id) => _orders.GetValueOrDefault(id);

    /// <inheritdoc/>
    public IReadOnlyList<Order> GetAll() => _orders.Values.OrderBy(o => o.Id).ToList();

    /// <inheritdoc/>
    public void Save()
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(_path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        List<OrderSnapshot> snapshots = _orders.Values.OrderBy(o => o.Id).Select(OrderSnapshot.Of).ToList();
        File.WriteAllText(_path, JsonSerializer.Serialize(snapshots, s_options));
    }

    private sealed record LineSnapshot(string MenuCode, int Quantity, decimal UnitPrice);

    private sealed record OrderSnapshot(
        int Id, int GuestId, DateTimeOffset CreatedAt, bool IsTableService, OrderStatus Status, List<LineSnapshot> Lines)
    {
        public static OrderSnapshot Of(Order o) => new(
            o.Id,
            o.GuestId,
            o.CreatedAt,
            o.IsTableService,
            o.Status,
            o.Lines.Select(l => new LineSnapshot(l.MenuCode, l.Quantity, l.UnitPrice)).ToList());

        public Order Restore()
        {
            var order = new Order(Id, GuestId, CreatedAt) { IsTableService = IsTableService };
            foreach (LineSnapshot line in Lines)
            {
                order.AddLine(new OrderLine(line.MenuCode, line.Quantity, line.UnitPrice));
            }

            Replay(order);
            return order;
        }

        // Стан відновлюється тими самими переходами, що й у роботі касира: неможливий стан не завантажиться.
        private void Replay(Order order)
        {
            if (Status == OrderStatus.Cancelled)
            {
                order.Cancel();
                return;
            }

            if (Status >= OrderStatus.Preparing)
            {
                order.StartPreparing();
            }

            if (Status >= OrderStatus.Served)
            {
                order.Serve();
            }

            if (Status == OrderStatus.Paid)
            {
                order.Pay();
            }
        }
    }
}
