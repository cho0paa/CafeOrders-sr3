using CafeOrders.Core.Domain;

namespace CafeOrders.Core.Abstractions;

/// <summary>Довідник меню: позиції й комплексні страви.</summary>
public interface IMenuCatalog
{
    /// <summary>Отримує всі позиції меню.</summary>
    IReadOnlyList<MenuItem> Items { get; }

    /// <summary>Отримує всі комплексні страви.</summary>
    IReadOnlyList<ComboMeal> Combos { get; }

    /// <summary>Шукає позицію меню за кодом без урахування регістру.</summary>
    /// <param name="code">Код позиції.</param>
    /// <returns>Позиція або null.</returns>
    MenuItem? FindItem(string code);

    /// <summary>Шукає комплекс за кодом без урахування регістру.</summary>
    /// <param name="code">Код комплексу.</param>
    /// <returns>Комплекс або null.</returns>
    ComboMeal? FindCombo(string code);
}
