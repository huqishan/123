using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Data;

namespace ControlLibrary.Converts
{
    public class ComparisonToVisibilityConvert : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string valueText = NormalizeText(value);
            string parameterText = NormalizeText(parameter);
            if (string.IsNullOrWhiteSpace(parameterText))
            {
                return Visibility.Collapsed;
            }

            parameterText = parameterText.Trim('\'', '"');

            bool invertResult = parameterText.StartsWith("!", StringComparison.Ordinal);
            if (invertResult)
            {
                parameterText = parameterText[1..].Trim();
            }

            bool isMatch = !string.IsNullOrWhiteSpace(valueText) &&
                           parameterText
                               .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                               .Any(item => string.Equals(item, valueText, StringComparison.OrdinalIgnoreCase));
            return (invertResult ? !isMatch : isMatch)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private static string NormalizeText(object? value)
        {
            string text = value?.ToString()?.Trim() ?? string.Empty;
            for (int index = 0; index < 3; index++)
            {
                string decodedText = System.Net.WebUtility.HtmlDecode(text)?.Trim() ?? string.Empty;
                if (string.Equals(decodedText, text, StringComparison.Ordinal))
                {
                    break;
                }

                text = decodedText;
            }

            return text;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
