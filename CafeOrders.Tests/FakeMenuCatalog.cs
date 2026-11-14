using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Domain;

namespace CafeOrders.Tests;

// Фейк довідника меню: кілька позицій і один комплекс у пам’яті.
internal sealed class FakeMenuCatalog : IMenuCatalog
{
    public FakeMenuCatalog()
    {
        var matcha = new MenuItem("TEA-005", "Матча лате", 85m, 300);
        matcha.Disable();
        Items = new List<MenuItem>
        {
            new("CAP-001", "Капучино", 65m, 250),
            new("CRO-001", "Круасан", 55m, 80),
            new("DES-002", "Тірамісу", 125m, 150),
            matcha,
        };
        Combos = new List<ComboMeal> { new("CMB-001", "Сніданок", new[] { "CAP-001", "CRO-001" }, 105m) };
    }

    public IReadOnlyList<MenuItem> Items { get; }

    public IReadOnlyList<ComboMeal> Combos { get; }

    public MenuItem? FindItem(string code) =>
        Items.FirstOrDefault(i => string.Equals(i.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));

    public ComboMeal? FindCombo(string code) =>
        Combos.FirstOrDefault(c => string.Equals(c.Code, code.Trim(), StringComparison.OrdinalIgnoreCase));
}
