namespace CafeOrders.Core.Domain;

/// <summary>
/// Комплексна страва: набір позицій меню за спільною ціною. Склад фіксується під час створення (X-01).
/// </summary>
public sealed class ComboMeal
{
    /// <summary>Найменша кількість позицій у комплексі.</summary>
    public const int MinComponentCount = 2;

    private readonly string[] _components;

    /// <summary>Ініціалізує комплекс і перевіряє інваріант.</summary>
    /// <param name="code">Код комплексу, непорожній.</param>
    /// <param name="title">Назва комплексу, непорожня.</param>
    /// <param name="components">Коди позицій меню, щонайменше дві.</param>
    /// <param name="price">Ціна комплексу, більша за нуль, грн.</param>
    /// <exception cref="ArgumentException">Код, назва порожні або позицій менше двох.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Ціна не додатна.</exception>
    public ComboMeal(string code, string title, IEnumerable<string> components, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(components);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(price);
        _components = components.ToArray();
        if (_components.Length < MinComponentCount)
        {
            throw new ArgumentException("Комплекс має містити щонайменше дві позиції.", nameof(components));
        }

        Code = code;
        Title = title;
        Price = price;
    }

    /// <summary>Отримує код комплексу.</summary>
    public string Code { get; }

    /// <summary>Отримує назву комплексу.</summary>
    public string Title { get; }

    /// <summary>Отримує ціну комплексу, грн.</summary>
    public decimal Price { get; }

    /// <summary>Отримує коди позицій меню, з яких складається комплекс, лише для читання.</summary>
    public IReadOnlyList<string> Components => _components;
}
