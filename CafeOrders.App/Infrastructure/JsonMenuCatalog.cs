using System.Text.Json;
using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Domain;

namespace CafeOrders.App.Infrastructure;

/// <summary>Довідник меню, завантажений з файлу JSON (data/menu.json).</summary>
internal sealed class JsonMenuCatalog : IMenuCatalog
{
    private static readonly JsonSerializerOptions s_options = new() { PropertyNameCaseInsensitive = true };

    private readonly Dictionary<string, MenuItem> _itemsByCode;
    private readonly Dictionary<string, ComboMeal> _combosByCode;

    private JsonMenuCatalog(IEnumerable<MenuItem> items, IEnumerable<ComboMeal> combos)
    {
        _itemsByCode = items.ToDictionary(i => i.Code, StringComparer.OrdinalIgnoreCase);
        _combosByCode = combos.ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public IReadOnlyList<MenuItem> Items => _itemsByCode.Values.ToList();

    /// <inheritdoc/>
    public IReadOnlyList<ComboMeal> Combos => _combosByCode.Values.ToList();

    /// <summary>Завантажує меню з файлу; дані перевіряються конструкторами MenuItem і ComboMeal.</summary>
    /// <param name="path">Шлях до файлу меню.</param>
    /// <returns>Довідник меню.</returns>
    /// <exception cref="IOException">Файл недоступний.</exception>
    /// <exception cref="JsonException">Файл пошкоджено.</exception>
    public static JsonMenuCatalog Load(string path)
    {
        MenuFile file = JsonSerializer.Deserialize<MenuFile>(File.ReadAllText(path), s_options)
            ?? throw new JsonException($"Файл меню {path} порожній");
        IEnumerable<MenuItem> items = file.Items.Select(i =>
        {
            var item = new MenuItem(i.Code, i.Title, i.Price, i.PortionGrams);
            if (!i.IsAvailable)
            {
                item.Disable();
            }

            return item;
        });
        IEnumerable<ComboMeal> combos = file.Combos.Select(c => new ComboMeal(c.Code, c.Title, c.Components, c.Price));
        return new JsonMenuCatalog(items, combos);
    }

    /// <inheritdoc/>
    public MenuItem? FindItem(string code) => _itemsByCode.GetValueOrDefault(code.Trim());

    /// <inheritdoc/>
    public ComboMeal? FindCombo(string code) => _combosByCode.GetValueOrDefault(code.Trim());

    private sealed record MenuFile(List<ItemDto> Items, List<ComboDto> Combos);

    private sealed record ItemDto(string Code, string Title, decimal Price, int PortionGrams, bool IsAvailable);

    private sealed record ComboDto(string Code, string Title, List<string> Components, decimal Price);
}
