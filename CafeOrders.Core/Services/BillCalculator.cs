using CafeOrders.Core.Abstractions;
using CafeOrders.Core.Domain;

namespace CafeOrders.Core.Services;

/// <summary>Профільне правило варіанта: рахунок із сервісним збором. Чистий розрахунок без побічних ефектів.</summary>
public sealed class BillCalculator
{
    private readonly IServiceChargePolicy _serviceCharge;

    /// <summary>Ініціалізує калькулятор політикою сервісного збору.</summary>
    /// <param name="serviceCharge">Правило сервісного збору.</param>
    public BillCalculator(IServiceChargePolicy serviceCharge)
    {
        _serviceCharge = serviceCharge ?? throw new ArgumentNullException(nameof(serviceCharge));
    }

    /// <summary>Розраховує рахунок замовлення. Не змінює замовлення.</summary>
    /// <param name="order">Замовлення.</param>
    /// <returns>Сума страв, сервісний збір і підсумок, округлені до копійок від нуля.</returns>
    public BillSummary BillOf(Order order)
    {
        ArgumentNullException.ThrowIfNull(order);
        decimal subtotal = 0m;
        foreach (OrderLine line in order.Lines)
        {
            subtotal += line.Amount;
        }

        subtotal = Math.Round(subtotal, 2, MidpointRounding.AwayFromZero);
        decimal charge = _serviceCharge.ChargeFor(subtotal, order.IsTableService);
        return new BillSummary(subtotal, charge, subtotal + charge);
    }
}
