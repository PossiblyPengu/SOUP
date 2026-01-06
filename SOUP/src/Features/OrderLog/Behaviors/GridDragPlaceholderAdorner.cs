using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace SOUP.Features.OrderLog.Behaviors;

internal sealed class GridDragPlaceholderAdorner : Adorner
{
    private Rect _rect;

    public GridDragPlaceholderAdorner(UIElement adornedElement) : base(adornedElement)
    {
        IsHitTestVisible = false;
        _rect = Rect.Empty;
    }

    public void Update(Rect rect)
    {
        _rect = rect;
        InvalidateVisual();
    }

    public void Clear()
    {
        _rect = Rect.Empty;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (_rect.IsEmpty) return;

        // Semi-transparent fill
        var fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255));
        fill.Freeze();

        // Dashed border
        var pen = new Pen(new SolidColorBrush(Color.FromArgb(200, 30, 144, 255)), 2);
        pen.DashStyle = new DashStyle(new double[] { 4, 3 }, 0);
        pen.Freeze();

        drawingContext.DrawRoundedRectangle(fill, pen, _rect, 6, 6);
    }
}
