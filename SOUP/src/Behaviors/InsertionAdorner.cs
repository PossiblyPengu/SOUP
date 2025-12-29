// InsertionAdorner removed: visual insertion indicator replaced by sliding animations.
// This stub remains to avoid compile issues if any references linger; it performs no rendering.
using System.Windows;
using System.Windows.Documents;

namespace SOUP.Behaviors;

public class InsertionAdorner : Adorner
{
    public InsertionAdorner(UIElement adornedElement) : base(adornedElement) { IsHitTestVisible = false; }
    public void UpdateY(double y) { }
    public void UpdateY(double y, string? label) { }
}
