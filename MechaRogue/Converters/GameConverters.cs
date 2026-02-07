namespace MechaRogue.Converters;

using System.Globalization;
using System.Windows.Data;

/// <summary>String equality → Visibility (for screen state switching).</summary>
public class StringMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Bool → Visibility.</summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool val = value is bool b && b;
        bool invert = parameter?.ToString() == "Invert";
        if (invert) val = !val;
        return val ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Armor percent (0-1) → color brush.</summary>
public class ArmorPercentToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = value is double d ? d : 1.0;
        if (pct <= 0) return new SolidColorBrush(Color.FromRgb(0x58, 0x58, 0x58)); // gray
        if (pct < 0.25) return new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)); // danger red
        if (pct < 0.55) return new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22)); // warning yellow
        return new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)); // success green
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>NodeType → display icon/symbol.</summary>
public class NodeTypeToIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            Models.NodeType.Battle => "⚔",
            Models.NodeType.EliteBattle => "🔥",
            Models.NodeType.Boss => "💀",
            Models.NodeType.Shop => "🛒",
            Models.NodeType.Rest => "🔧",
            Models.NodeType.Event => "❓",
            _ => "•"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>NodeType → accent color.</summary>
public class NodeTypeToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            Models.NodeType.Battle => new SolidColorBrush(Color.FromRgb(0x58, 0xA6, 0xFF)),
            Models.NodeType.EliteBattle => new SolidColorBrush(Color.FromRgb(0xD2, 0x99, 0x22)),
            Models.NodeType.Boss => new SolidColorBrush(Color.FromRgb(0xF8, 0x51, 0x49)),
            Models.NodeType.Shop => new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)),
            Models.NodeType.Rest => new SolidColorBrush(Color.FromRgb(0x3F, 0xB9, 0x50)),
            Models.NodeType.Event => new SolidColorBrush(Color.FromRgb(0xBC, 0x8C, 0xFF)),
            _ => new SolidColorBrush(Color.FromRgb(0x8B, 0x94, 0x9E))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Part cost calculator for display: 30 + Tier * 20.</summary>
public class PartCostConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.MedaPart part)
            return $"{30 + part.Tier * 20}¢";
        return "??¢";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Medaforce charge → percentage display.</summary>
public class MedaforceChargeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2 && values[0] is int charge && values[1] is int max && max > 0)
            return (double)charge / max * 100.0;
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>BattlePhase equality → Visibility.</summary>
public class BattlePhaseToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.BattlePhase phase && Enum.TryParse<Models.BattlePhase>(parameter?.ToString(), out var target))
            return phase == target ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
