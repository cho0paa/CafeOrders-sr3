using System.Globalization;
using System.Text;
using CafeOrders.Core.Domain;

namespace CafeOrders.Core.Reporting;

/// <summary>Обов’язковий звіт варіанта «популярність позицій» за оплаченими замовленнями.</summary>
public static class PopularityReport
{
    /// <summary>Рахує порції й виторг за кожним кодом меню в оплачених замовленнях.</summary>
    /// <param name="orders">Замовлення будь-яких станів; враховуються лише Paid.</param>
    /// <returns>Рядки звіту за спаданням порцій, за рівності — за кодом; порожній список, якщо оплат немає.</returns>
    public static IReadOnlyList<PopularityRow> Build(IEnumerable<Order> orders)
    {
        ArgumentNullException.ThrowIfNull(orders);
        var byCode = new Dictionary<string, (int Portions, decimal Revenue)>(StringComparer.OrdinalIgnoreCase);
        foreach (Order order in orders.Where(o => o.Status == OrderStatus.Paid))
        {
            foreach (OrderLine line in order.Lines)
            {
                byCode.TryGetValue(line.MenuCode, out var acc);
                byCode[line.MenuCode] = (acc.Portions + line.Quantity, acc.Revenue + line.Amount);
            }
        }

        return byCode
            .Select(p => new PopularityRow(p.Key, p.Value.Portions, p.Value.Revenue))
            .OrderByDescending(r => r.Portions)
            .ThenBy(r => r.MenuCode, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Форматує звіт таблицею: код, порції, виторг.</summary>
    /// <param name="rows">Рядки звіту.</param>
    /// <returns>Текст звіту; при порожньому списку — повідомлення про відсутність оплат.</returns>
    public static string TextOf(IReadOnlyList<PopularityRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        if (rows.Count == 0)
        {
            return "Оплачених замовлень немає." + Environment.NewLine;
        }

        CultureInfo culture = CultureInfo.InvariantCulture;
        var text = new StringBuilder();
        text.AppendLine(culture, $"{"Код",-10}{"Порцій",8}{"Виторг",12}");
        foreach (PopularityRow row in rows)
        {
            text.AppendLine(culture, $"{row.MenuCode,-10}{row.Portions,8}{row.Revenue,12:F2}");
        }

        return text.ToString();
    }
}
