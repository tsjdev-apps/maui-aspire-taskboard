using System.Globalization;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.Maui.Converters;

public sealed class SyncTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTimeOffset time ? $"{Text.LastSync}: {time.ToString("g", culture)}" : Text.NeverSynced;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
