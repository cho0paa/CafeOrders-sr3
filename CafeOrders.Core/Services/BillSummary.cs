namespace CafeOrders.Core.Services;

/// <summary>Рахунок замовлення.</summary>
/// <param name="Subtotal">Сума страв, грн.</param>
/// <param name="ServiceCharge">Сервісний збір, грн.</param>
/// <param name="Total">Підсумок до сплати, грн.</param>
public sealed record BillSummary(decimal Subtotal, decimal ServiceCharge, decimal Total);
