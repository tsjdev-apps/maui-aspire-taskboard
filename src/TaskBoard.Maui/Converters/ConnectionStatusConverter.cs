using System.Globalization;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.Maui.Converters;

public sealed class ConnectionStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Text.Connected : Text.Disconnected;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
