using System.Globalization;

namespace Presentation.Converters
{
    public class ConfidenceToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is float confidence)
            {
                return confidence switch
                {
                    > 0.8f => Color.FromArgb("#4CAF50"), // Green
                    > 0.6f => Color.FromArgb("#FFEB3B"), // Yellow
                    > 0.4f => Color.FromArgb("#FF9800"), // Orange
                    _ => Color.FromArgb("#F44336")       // Red
                };
            }
            return Colors.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}