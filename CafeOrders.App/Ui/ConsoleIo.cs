using System.Globalization;
using CafeOrders.Core.Domain;

namespace CafeOrders.App.Ui;

/// <summary>
/// Барикада вводу: перетворює рядки касира на значення; некоректний ввід — пояснення й повторний запит.
/// </summary>
internal sealed class ConsoleIo
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly bool _echo;

    /// <summary>Ініціалізує ввід і вивід.</summary>
    /// <param name="input">Джерело вводу.</param>
    /// <param name="output">Куди писати.</param>
    /// <param name="echo">Повторювати введене у виводі (коли ввід перенаправлено з файлу).</param>
    public ConsoleIo(TextReader input, TextWriter output, bool echo)
    {
        _input = input;
        _output = output;
        _echo = echo;
    }

    /// <summary>Друкує текст.</summary>
    /// <param name="text">Текст.</param>
    public void Write(string text) => _output.Write(text);

    /// <summary>Читає рядок; null — кінець вводу.</summary>
    /// <returns>Рядок або null.</returns>
    public string? ReadLine()
    {
        string? line = _input.ReadLine();
        if (_echo && line is not null)
        {
            _output.WriteLine(line);
        }

        return line;
    }

    /// <summary>Читає ціле число більше нуля.</summary>
    /// <param name="prompt">Запрошення.</param>
    /// <returns>Число.</returns>
    public int ReadPositiveInt(string prompt) =>
        AskValue<int>(prompt, s => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0 ? v : (int?)null, "ціле число більше 0");

    /// <summary>Читає суму грошей з крапкою як роздільником.</summary>
    /// <param name="prompt">Запрошення.</param>
    /// <returns>Сума, не менша за нуль.</returns>
    public decimal ReadMoney(string prompt) =>
        AskValue<decimal>(prompt, s => decimal.TryParse(s, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal v) ? v : (decimal?)null, "сума з крапкою, наприклад 150.50");

    /// <summary>Читає непорожній рядок.</summary>
    /// <param name="prompt">Запрошення.</param>
    /// <returns>Рядок без пробілів по краях.</returns>
    public string ReadNonEmpty(string prompt) =>
        AskText(prompt, s => string.IsNullOrWhiteSpace(s) ? null : s.Trim(), "непорожнє значення");

    /// <summary>Читає відповідь «так» або «ні».</summary>
    /// <param name="prompt">Запрошення.</param>
    /// <returns>true для «так».</returns>
    public bool ReadYesNo(string prompt) =>
        AskValue<bool>(prompt, s => s?.Trim().ToUpperInvariant() switch { "ТАК" => true, "НІ" => false, _ => (bool?)null }, "«так» або «ні»");

    /// <summary>Читає необов’язкове ціле число; порожньо — null.</summary>
    /// <param name="prompt">Запрошення.</param>
    /// <returns>Число або null.</returns>
    public int? ReadOptionalInt(string prompt)
    {
        while (true)
        {
            _output.Write(prompt);
            string? s = ReadLine();
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) && v > 0)
            {
                return v;
            }

            _output.WriteLine("Помилка вводу: ціле число більше 0 або порожньо");
        }
    }

    /// <summary>Читає необов’язковий стан замовлення; порожньо — null.</summary>
    /// <param name="prompt">Запрошення.</param>
    /// <returns>Стан або null.</returns>
    public OrderStatus? ReadOptionalStatus(string prompt)
    {
        while (true)
        {
            _output.Write(prompt);
            string? s = ReadLine();
            if (string.IsNullOrWhiteSpace(s))
            {
                return null;
            }

            if (Enum.TryParse(s.Trim(), ignoreCase: true, out OrderStatus status) && Enum.IsDefined(status))
            {
                return status;
            }

            _output.WriteLine("Помилка вводу: невідомий стан");
        }
    }

    private T AskValue<T>(string prompt, Func<string?, T?> parse, string hint)
        where T : struct
    {
        while (true)
        {
            _output.Write(prompt);
            string? s = ReadLine() ?? throw new EndOfStreamException("Ввід закінчився посеред сценарію.");
            T? value = parse(s);
            if (value.HasValue)
            {
                return value.Value;
            }

            _output.WriteLine($"Помилка вводу: потрібне {hint}");
        }
    }

    private string AskText(string prompt, Func<string?, string?> parse, string hint)
    {
        while (true)
        {
            _output.Write(prompt);
            string? s = ReadLine() ?? throw new EndOfStreamException("Ввід закінчився посеред сценарію.");
            string? value = parse(s);
            if (value is not null)
            {
                return value;
            }

            _output.WriteLine($"Помилка вводу: потрібне {hint}");
        }
    }
}
