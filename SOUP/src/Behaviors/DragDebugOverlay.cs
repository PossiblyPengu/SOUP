using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SOUP.Behaviors
{
    // Lightweight topmost, click-through overlay window for showing recent drag debug lines.
    internal static class DragDebugOverlay
    {
        private static Window? _win;
        private static TextBlock? _text;
        private static StringBuilder _sb = new();

        public static void Show()
        {
            try
            {
                if (_win != null) return;
                var main = Application.Current?.MainWindow;
                _win = new Window
                {
                    Owner = main,
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = Brushes.Transparent,
                    IsHitTestVisible = false,
                    ShowInTaskbar = false,
                    Topmost = true,
                    Width = main?.ActualWidth ?? 400,
                    Height = 60,
                    Left = main?.Left ?? 100,
                    Top = (main?.Top ?? 100) + 10,
                    ShowActivated = false
                };

                var border = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(6),
                    Child = (_text = new TextBlock
                    {
                        Foreground = Brushes.White,
                        FontSize = 12,
                        TextWrapping = TextWrapping.Wrap
                    })
                };

                _win.Content = border;
                _win.Show();
            }
            catch { }
        }

        public static void Hide()
        {
            try
            {
                if (_win == null) return;
                _win.Close();
                _win = null;
                _text = null;
                _sb.Clear();
            }
            catch { }
        }

        public static void AppendLine(string line)
        {
            try
            {
                _sb.AppendLine(line);
                // keep only last ~5 lines
                var all = _sb.ToString().Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                int start = Math.Max(0, all.Length - 5);
                var recent = string.Join("\n", all, start, all.Length - start);
                if (_text != null)
                {
                    _text.Text = recent;
                }
                // ensure visible
                if (_win == null) Show();
            }
            catch { }
        }
    }
}
