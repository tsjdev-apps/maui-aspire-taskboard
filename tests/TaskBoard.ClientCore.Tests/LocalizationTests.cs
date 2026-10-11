using System.Globalization;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.ClientCore.Tests;

public sealed class LocalizationTests
{
    [Theory]
    [InlineData("en-US", "Tasks", "Save")]
    [InlineData("de-DE", "Aufgaben", "Speichern")]
    public void ResourcesFollowUiCulture(string culture, string tasks, string save)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            Assert.Equal(tasks, Text.Tasks);
            Assert.Equal(save, Text.Save);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }
}
