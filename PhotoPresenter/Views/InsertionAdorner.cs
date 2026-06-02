using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PhotoPresenter.Views;

/// <summary>
/// Draws a 2-px blue insertion line at the top or bottom edge of the adorned element.
/// </summary>
internal sealed class InsertionAdorner : Adorner
{
    private static readonly Pen LinePen = MakePen();
    public bool InsertBefore { get; }
    private readonly bool _insertBefore;

    public InsertionAdorner(UIElement adornedElement, bool insertBefore) : base(adornedElement)
    {
        InsertBefore = insertBefore;
        _insertBefore = insertBefore;
        IsHitTestVisible = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var fe = (FrameworkElement)AdornedElement;
        double y = _insertBefore ? 1 : fe.ActualHeight - 1;
        double w = fe.ActualWidth;

        dc.DrawLine(LinePen, new Point(0, y), new Point(w, y));
        // Small arrow nubs at each end
        dc.DrawEllipse(Brushes.DodgerBlue, null, new Point(4, y), 4, 4);
        dc.DrawEllipse(Brushes.DodgerBlue, null, new Point(w - 4, y), 4, 4);
    }

    private static Pen MakePen()
    {
        var p = new Pen(Brushes.DodgerBlue, 2);
        p.Freeze();
        return p;
    }
}
