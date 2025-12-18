using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SOUP.Features.OrderLog.Models;
using SOUP.Features.OrderLog.ViewModels;

namespace SOUP.Features.OrderLog.Helpers;

/// <summary>
/// Helper class for text formatting operations on note content TextBoxes.
/// Eliminates duplicate code between OrderLogView and OrderLogWidgetView.
/// </summary>
public static class TextFormattingHelper
{
    /// <summary>
    /// Finds the NoteContent TextBox by walking up the visual tree from a button.
    /// </summary>
    public static TextBox? FindNoteContentTextBox(object sender)
    {
        if (sender is not Button button) return null;

        // Navigate up to find the parent container and then find the TextBox
        var parent = VisualTreeHelper.GetParent(button);
        while (parent != null)
        {
            if (parent is Border border && border.Parent is StackPanel stackPanel)
            {
                // Find the Grid containing the TextBox
                foreach (var child in stackPanel.Children)
                {
                    if (child is Grid grid)
                    {
                        foreach (var gridChild in grid.Children)
                        {
                            if (gridChild is TextBox tb)
                                return tb;
                        }
                    }
                }
            }
            parent = VisualTreeHelper.GetParent(parent);
        }
        return null;
    }

    /// <summary>
    /// Wraps the selected text (or "text" if nothing selected) with prefix and suffix.
    /// </summary>
    public static void WrapSelectedText(TextBox textBox, string prefix, string suffix)
    {
        var start = textBox.SelectionStart;
        var length = textBox.SelectionLength;
        var text = textBox.Text ?? "";
        var selectedText = length > 0 ? textBox.SelectedText : "text";

        var newText = text.Substring(0, start) + prefix + selectedText + suffix + text.Substring(start + length);
        textBox.Text = newText;

        // Position cursor after the formatted text
        textBox.SelectionStart = start + prefix.Length + selectedText.Length + suffix.Length;
        textBox.Focus();
    }

    /// <summary>
    /// Inserts text at the cursor position, optionally ensuring it's on a new line.
    /// </summary>
    public static void InsertTextAtCursor(TextBox textBox, string insertText, bool newLine = false)
    {
        var start = textBox.SelectionStart;
        var text = textBox.Text ?? "";

        // If newLine is true, ensure we're at the start of a line
        var prefix = "";
        if (newLine && start > 0 && text[start - 1] != '\n')
        {
            prefix = "\n";
        }

        var newText = text.Substring(0, start) + prefix + insertText + text.Substring(start + textBox.SelectionLength);
        textBox.Text = newText;
        textBox.SelectionStart = start + prefix.Length + insertText.Length;
        textBox.Focus();
    }

    /// <summary>
    /// Updates the NoteContent property on the OrderItem and saves to database.
    /// </summary>
    public static async void UpdateNoteContent(object sender, FrameworkElement view)
    {
        if (sender is Button button && button.Tag is OrderItem order)
        {
            var textBox = FindNoteContentTextBox(sender);
            if (textBox != null)
            {
                order.NoteContent = textBox.Text;
                if (view.DataContext is OrderLogViewModel vm)
                {
                    await vm.SaveAsync();
                }
            }
        }
    }

    /// <summary>
    /// Handles auto-continuation of list items (bullets, checkboxes, numbered lists) when Enter is pressed.
    /// </summary>
    public static void HandleListAutoContinuation(TextBox textBox, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        var text = textBox.Text ?? "";
        var caretIndex = textBox.CaretIndex;

        // Find the start of the current line
        var lineStart = text.LastIndexOf('\n', Math.Max(0, caretIndex - 1)) + 1;
        var currentLine = text.Substring(lineStart, caretIndex - lineStart);

        // Check for list prefixes
        string? prefix = DetectListPrefix(currentLine);

        if (prefix != null)
        {
            // Check if current line only has the prefix (empty list item)
            var trimmedLine = currentLine.TrimStart();
            var isEmptyListItem = IsEmptyListItem(trimmedLine);

            if (isEmptyListItem)
            {
                // Remove the empty list item and just add a newline
                var newText = text.Substring(0, lineStart) + text.Substring(caretIndex);
                textBox.Text = newText;
                textBox.CaretIndex = lineStart;
            }
            else
            {
                // Insert newline with prefix
                var newText = text.Substring(0, caretIndex) + "\n" + prefix + text.Substring(caretIndex);
                textBox.Text = newText;
                textBox.CaretIndex = caretIndex + 1 + prefix.Length;
            }

            e.Handled = true;
        }
    }

    private static string? DetectListPrefix(string currentLine)
    {
        var indent = currentLine.Length - currentLine.TrimStart().Length;
        var trimmed = currentLine.TrimStart();

        // Check for bullet point (• )
        if (trimmed.StartsWith("• "))
            return new string(' ', indent) + "• ";

        // Check for checkbox empty (☐ )
        if (trimmed.StartsWith("☐ "))
            return new string(' ', indent) + "☐ ";

        // Check for checkbox checked (☑ ) - reset to empty checkbox
        if (trimmed.StartsWith("☑ "))
            return new string(' ', indent) + "☐ ";

        // Check for dash list (- )
        if (trimmed.StartsWith("- "))
            return new string(' ', indent) + "- ";

        // Check for numbered list (1. , 2. , etc.)
        var match = Regex.Match(trimmed, @"^(\d+)\.\s");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
            return new string(' ', indent) + $"{num + 1}. ";

        return null;
    }

    private static bool IsEmptyListItem(string trimmedLine)
    {
        return trimmedLine == "• " ||
               trimmedLine == "☐ " ||
               trimmedLine == "☑ " ||
               trimmedLine == "- " ||
               Regex.IsMatch(trimmedLine, @"^\d+\.\s*$");
    }

    // Click handler methods that can be called from code-behind
    public static void FormatBold(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            WrapSelectedText(textBox, "**", "**");
            UpdateNoteContent(sender, view);
        }
    }

    public static void FormatItalic(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            WrapSelectedText(textBox, "*", "*");
            UpdateNoteContent(sender, view);
        }
    }

    public static void FormatUnderline(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            WrapSelectedText(textBox, "__", "__");
            UpdateNoteContent(sender, view);
        }
    }

    public static void InsertBullet(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            InsertTextAtCursor(textBox, "• ", newLine: true);
            UpdateNoteContent(sender, view);
        }
    }

    public static void InsertCheckbox(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            InsertTextAtCursor(textBox, "☐ ", newLine: true);
            UpdateNoteContent(sender, view);
        }
    }

    public static void InsertTimestamp(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            var timestamp = DateTime.Now.ToString("MM/dd HH:mm");
            InsertTextAtCursor(textBox, $"[{timestamp}] ");
            UpdateNoteContent(sender, view);
        }
    }

    public static void InsertDivider(object sender, FrameworkElement view)
    {
        var textBox = FindNoteContentTextBox(sender);
        if (textBox != null)
        {
            InsertTextAtCursor(textBox, "────────────", newLine: true);
            UpdateNoteContent(sender, view);
        }
    }
}
