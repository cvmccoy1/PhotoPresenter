using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentMode))
            ApplyMode(_vm.CurrentMode);
    }

    private void ApplyMode(AppMode mode)
    {
        if (mode == AppMode.Present)
        {
            MainToolbar.Visibility = Visibility.Collapsed;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Dispatcher.InvokeAsync(Focus, DispatcherPriority.Input);
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = WindowState.Normal;
            MainToolbar.Visibility = Visibility.Visible;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_vm.CurrentMode != AppMode.Present) return;

        var pvm = _vm.PresentVM;
        switch (e.Key)
        {
            case Key.Right:
            case Key.Space:
                pvm.NextPhoto();
                e.Handled = true;
                break;
            case Key.Left:
                pvm.PreviousPhoto();
                e.Handled = true;
                break;
            case Key.Add:
            case Key.OemPlus:
                pvm.ZoomIn();
                e.Handled = true;
                break;
            case Key.Subtract:
            case Key.OemMinus:
                pvm.ZoomOut();
                e.Handled = true;
                break;
            case Key.Escape:
                _vm.SwitchToOrganise();
                e.Handled = true;
                break;
        }
    }
}
