using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace ParrotPet.Views;

/// <summary>Строка с hex-цветом → кисть (для мини-иконок скинов в списках).</summary>
public class HexBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            if (value is string hex) return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch (FormatException)
        {
            // Некорректный цвет — показываем прозрачным
        }
        return Brushes.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}