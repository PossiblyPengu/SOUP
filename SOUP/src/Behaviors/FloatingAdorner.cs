using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SOUP.Behaviors;

public class FloatingAdorner : Adorner
{
    private readonly Image _image;
    private double _left;
    private double _top;

    public FloatingAdorner(UIElement adornedElement, ImageSource bitmap, double desiredWidth, double desiredHeight) : base(adornedElement)
    {
        double w = desiredWidth > 0 ? desiredWidth : (bitmap is BitmapSource bs ? bs.PixelWidth : 0);
        double h = desiredHeight > 0 ? desiredHeight : (bitmap is BitmapSource bs2 ? bs2.PixelHeight : 0);
        _image = new Image
        {
            Source = bitmap,
            Width = w,
            Height = h,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
            Opacity = 0.98
        };
        AddVisualChild(_image);
        IsHitTestVisible = false;
    }

    // Expose the rendered image size so callers can align animations with the floating snapshot
    public double AdornedWidth => _image.Width;
    public double AdornedHeight => _image.Height;

    protected override int VisualChildrenCount => 1;
    protected override Visual GetVisualChild(int index) => _image;

    protected override Size MeasureOverride(Size constraint)
    {
        _image.Measure(constraint);
        return _image.DesiredSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        _image.Arrange(new Rect(_left, _top, _image.Width, _image.Height));
        return finalSize;
    }

    public void UpdatePosition(double left, double top)
    {
        _left = left;
        _top = top;
        InvalidateArrange();
    }
}
