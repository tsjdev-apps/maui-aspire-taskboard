using System.Globalization;
using TaskBoard.ClientCore.Resources;

namespace TaskBoard.Maui.Converters;

public sealed class TaskStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Text.Completed : Text.Incomplete;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
