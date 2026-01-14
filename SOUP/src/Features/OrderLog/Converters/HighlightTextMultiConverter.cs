using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace SOUP.Features.OrderLog.Converters
{
    /// <summary>
    /// MultiValueConverter that takes [text, query] and returns a TextBlock with matched runs highlighted.
    /// </summary>
    public class HighlightTextMultiConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            var text = values?.Length > 0 ? values[0] as string ?? string.Empty : string.Empty;
            var query = values?.Length > 1 ? values[1] as string ?? string.Empty : string.Empty;

            var tb = new TextBlock();
            tb.TextTrimming = TextTrimming.CharacterEllipsis;
            // Try to inherit card font size from application resources for consistency
            try
            {
                if (Application.Current != null && Application.Current.Resources.Contains("CardFontSize"))
                {
                    var fs = Application.Current.Resources["CardFontSize"];
                    if (fs is double d) tb.FontSize = d;
                    else if (fs is float f) tb.FontSize = f;
                }
            }
            catch { }

            tb.FontWeight = FontWeights.SemiBold;
            tb.VerticalAlignment = VerticalAlignment.Center;

            if (string.IsNullOrEmpty(query))
            {
                tb.Text = text;
                return tb;
            }

            try
            {
                var comparison = StringComparison.OrdinalIgnoreCase;
                int idx = 0;
                while (idx < text.Length)
                {
                    int found = text.IndexOf(query, idx, comparison);
                    if (found < 0)
                    {
                        var run = new Run(text.Substring(idx));
                        run.Foreground = GetBrush("TextPrimaryBrush", Brushes.White);
                        run.FontWeight = FontWeights.Normal;
                        tb.Inlines.Add(run);
                        break;
                    }
                    if (found > idx)
                    {
                        var run = new Run(text.Substring(idx, found - idx));
                        run.Foreground = GetBrush("TextPrimaryBrush", Brushes.White);
                        run.FontWeight = FontWeights.Normal;
                        tb.Inlines.Add(run);
                    }
                    var matchRun = new Run(text.Substring(found, query.Length));
                    matchRun.FontWeight = FontWeights.SemiBold;
                    matchRun.Foreground = GetBrush("AccentBrush", Brushes.Yellow);
                    tb.Inlines.Add(matchRun);
                    idx = found + query.Length;
                }
            }
            catch
            {
                tb.Text = text; // fallback
            }

            return tb;
        }

        private Brush GetBrush(string key, Brush fallback)
        {
            try
            {
                if (Application.Current != null && Application.Current.Resources.Contains(key))
                {
                    var b = Application.Current.Resources[key] as Brush;
                    if (b != null) return b;
                }
            }
            catch { }
            return fallback;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
