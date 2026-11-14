using System.Globalization;
using Microsoft.Extensions.Logging;

namespace CafeOrders.App.Infrastructure;

/// <summary>
/// Журнал у файл: рядок «час;рівень;сценарій;повідомлення». Персональних даних і вмісту файлів не пише.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly StreamWriter _writer;
    private readonly object _gate = new();

    /// <summary>Відкриває файл журналу на дописування.</summary>
    /// <param name="path">Шлях до файлу журналу.</param>
    public FileLoggerProvider(string path)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(path, append: true) { AutoFlush = true };
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) => new FileLogger(this, categoryName);

    /// <inheritdoc/>
    public void Dispose() => _writer.Dispose();

    private void Write(string line)
    {
        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    private sealed class FileLogger : ILogger
    {
        private readonly FileLoggerProvider _owner;
        private readonly string _category;
        private readonly AsyncLocal<string?> _scope = new();

        public FileLogger(FileLoggerProvider owner, string category)
        {
            _owner = owner;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            string? previous = _scope.Value;
            _scope.Value = state.ToString();
            return new ScopeReset(() => _scope.Value = previous);
        }

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            string time = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            string scenario = _scope.Value ?? _category;
            string error = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}";
            _owner.Write($"{time};{logLevel};{scenario};{formatter(state, exception)}{error}");
        }
    }

    private sealed class ScopeReset : IDisposable
    {
        private readonly Action _reset;

        public ScopeReset(Action reset) => _reset = reset;

        public void Dispose() => _reset();
    }
}
