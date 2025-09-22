using System.Globalization;

namespace Presentation.Converters
{
    public class DetectionButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool boolValue && boolValue ? "⏹️ Остановить детекцию" : "▶️ Начать детекцию";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}