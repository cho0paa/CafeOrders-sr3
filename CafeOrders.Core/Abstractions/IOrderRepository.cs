using CafeOrders.Core.Domain;

namespace CafeOrders.Core.Abstractions;

/// <summary>Сховище замовлень. Відповідає лише за зберігання — без листів і звітів.</summary>
public interface IOrderRepository
{
    /// <summary>Додає нове замовлення.</summary>
    /// <param name="order">Замовлення з унікальним номером.</param>
    void Add(Order order);

    /// <summary>Шукає замовлення за номером.</summary>
    /// <param name="id">Номер замовлення.</param>
    /// <returns>Замовлення або null, якщо його немає.</returns>
    Order? GetById(int id);

    /// <summary>Повертає всі замовлення.</summary>
    /// <returns>Знімок списку замовлень на момент виклику.</returns>
    IReadOnlyList<Order> GetAll();

    /// <summary>
    /// Зберігає поточний стан замовлень. Сховищу в пам’яті зберігати нічого — реалізація за замовчуванням порожня.
    /// </summary>
    void Save()
    {
    }
}
