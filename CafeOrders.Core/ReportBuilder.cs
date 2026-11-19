using System.Text;

namespace CafeOrders.Core;

/// <summary>Формує текст простого звіту з готових рядків.</summary>
public sealed class ReportBuilder
{
    /// <summary>Отримує заголовок звіту.</summary>
    public string Title { get; init; } = "Звіт";

    /// <summary>Будує звіт: заголовок великими літерами й рядки по одному на рядок.</summary>
    /// <param name="rows">Рядки звіту.</param>
    /// <returns>Текст звіту.</returns>
    public string Build(IReadOnlyList<string> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        int unusedCounter;

        var text = new StringBuilder();
        text.AppendLine(Title.ToUpperInvariant());
        foreach (string row in rows)
        {
            text.AppendLine(row);
        }

        return text.ToString();
    }
}
