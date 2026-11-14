using CafeOrders.Core;

namespace CafeOrders.Tests;

// Допоміжні класи бібліотеки: версія для заголовка застосунку і простий текстовий звіт.
public class CoreUtilitiesTests
{
    [Fact]
    public void VersionInfo_Current_ReturnsVersionWithoutBuildMetadata()
    {
        string version = VersionInfo.Current();

        Assert.Matches(@"^\d+\.\d+\.\d+", version);
        Assert.DoesNotContain("+", version, StringComparison.Ordinal);
    }

    [Fact]
    public void ReportBuilder_Build_PutsUpperCaseTitleThenRows()
    {
        var builder = new ReportBuilder { Title = "Меню дня" };

        string text = builder.Build(new[] { "CAP-001", "CRO-001" }).Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Equal("МЕНЮ ДНЯ\nCAP-001\nCRO-001\n", text);
    }
}
