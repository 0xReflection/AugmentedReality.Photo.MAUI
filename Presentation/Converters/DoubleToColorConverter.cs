using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Presentation.Converters
{
    public class DoubleToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double confidence)
            {
                if (confidence < 0.4)
                    return Colors.Red;
                else if (confidence < 0.6)
                    return Colors.Orange;
                else if (confidence < 0.8)
                    return Colors.Yellow;
                else if (confidence < 0.95)
                    return Colors.LightGreen;
                else
                    return Colors.Green;
            }

            return Colors.Gray; // fallback
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
