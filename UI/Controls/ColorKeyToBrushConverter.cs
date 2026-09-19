using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SmartNotes.Core.Models;

namespace SmartNotes.UI.Controls;

public class ColorKeyToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string key)
        {
            var theme = NoteColorTheme.Get(key);
            try
            {
                return new SolidColorBrush((Color)ColorConverter.ConvertFromString(theme.PrimaryHex));
            }
            catch { }
        }
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
