using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Views;

public partial class PresentView : UserControl
{
    public PresentView() => InitializeComponent();

    private PresentViewModel? Vm => DataContext as PresentViewModel;

    private void RootGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        Vm?.ZoomByDelta(e.Delta);
        e.Handled = true;
    }

    private void RootGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Vm?.BeginPan(e.GetPosition(this));
        RootGrid.CaptureMouse();
    }

    private void RootGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
            Vm?.UpdatePan(e.GetPosition(this));
    }

    private void RootGrid_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        RootGrid.ReleaseMouseCapture();
    }
}
