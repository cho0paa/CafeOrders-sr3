namespace CafeOrders.Core.Reporting;

/// <summary>Рядок звіту «популярність позицій».</summary>
/// <param name="MenuCode">Код позиції меню або комплексу.</param>
/// <param name="Portions">Продано порцій.</param>
/// <param name="Revenue">Виторг за позицією без сервісного збору, грн.</param>
public sealed record PopularityRow(string MenuCode, int Portions, decimal Revenue);
