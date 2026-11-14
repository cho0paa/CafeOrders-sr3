using System.Reflection;

namespace CafeOrders.Core;

/// <summary>Версія збірки з Directory.Build.props.</summary>
public static class VersionInfo
{
    /// <summary>Повертає семантичну версію без метаданих складання (частини після «+»).</summary>
    /// <returns>Рядок версії, наприклад 1.0.0.</returns>
    public static string Current()
    {
        Assembly assembly = typeof(VersionInfo).Assembly;
        var attribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        string raw = attribute?.InformationalVersion ?? "0.0.0";
        int plus = raw.IndexOf('+', StringComparison.Ordinal);
        return plus < 0 ? raw : raw[..plus];
    }
}
