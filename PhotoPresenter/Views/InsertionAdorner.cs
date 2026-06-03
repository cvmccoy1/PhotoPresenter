using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace PhotoPresenter.Views;

/// <summary>
/// Draws a 2-px blue insertion line at an edge of the adorned element.
/// Horizontal mode (folder list): top or bottom edge.
/// Vertical mode (photo grid):    left or right edge.
/// </summary>
internal sealed class InsertionAdorner : Adorner
{
    private static readonly Pen LinePen = MakePen();

    public bool InsertBefore { get; }
    public bool Vertical     { get; }

    public InsertionAdorner(UIElement adornedElement, bool insertBefore, bool vertical = false)
        : base(adornedElement)
    {
        InsertBefore      = insertBefore;
        Vertical          = vertical;
        IsHitTestVisible  = false;
    }

    protected override void OnRender(DrawingContext dc)
    {
        var fe = (FrameworkElement)AdornedElement;

        if (Vertical)
        {
            double x = InsertBefore ? 1 : fe.ActualWidth - 1;
            double h = fe.ActualHeight;
            dc.DrawLine(LinePen, new Point(x, 0), new Point(x, h));
            dc.DrawEllipse(Brushes.DodgerBlue, null, new Point(x, 4),      4, 4);
            dc.DrawEllipse(Brushes.DodgerBlue, null, new Point(x, h - 4),  4, 4);
        }
        else
        {
            double y = InsertBefore ? 1 : fe.ActualHeight - 1;
            double w = fe.ActualWidth;
            dc.DrawLine(LinePen, new Point(0, y), new Point(w, y));
            dc.DrawEllipse(Brushes.DodgerBlue, null, new Point(4,     y),  4, 4);
            dc.DrawEllipse(Brushes.DodgerBlue, null, new Point(w - 4, y),  4, 4);
        }
    }

    private static Pen MakePen()
    {
        var p = new Pen(Brushes.DodgerBlue, 2);
        p.Freeze();
        return p;
    }
}
