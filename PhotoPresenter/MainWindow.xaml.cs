using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using PhotoPresenter.Services;
using PhotoPresenter.ViewModels;
using PhotoPresenter.Views;

namespace PhotoPresenter;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly UserSettings _settings;
    private WindowState _organiseWindowState = WindowState.Normal;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        _vm.PropertyChanged += OnVmPropertyChanged;

        _settings = UserSettings.Load();
        RestoreWindowBounds();
    }

    private void RestoreWindowBounds()
    {
        if (_settings.WindowLeft.HasValue && _settings.WindowTop.HasValue)
        {
            Left = _settings.WindowLeft.Value;
            Top  = _settings.WindowTop.Value;
        }
        if (_settings.WindowWidth.HasValue && _settings.WindowHeight.HasValue)
        {
            Width  = _settings.WindowWidth.Value;
            Height = _settings.WindowHeight.Value;
        }
        if (_settings.WindowMaximized)
        {
            _organiseWindowState = WindowState.Maximized;
            WindowState = WindowState.Maximized;
        }
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        // Reload to pick up any mid-session saves (e.g. splitter position).
        var settings = UserSettings.Load();

        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, Width, Height)
            : RestoreBounds;

        if (!bounds.IsEmpty)
        {
            settings.WindowLeft   = bounds.Left;
            settings.WindowTop    = bounds.Top;
            settings.WindowWidth  = bounds.Width;
            settings.WindowHeight = bounds.Height;
        }
        settings.WindowMaximized    = _organiseWindowState == WindowState.Maximized;
        settings.LastSelectedFolder = _vm.OrganiseVM.SelectedFolder?.Name ?? "";
        settings.LastSelectedPhoto  = _vm.OrganiseVM.SelectedPhoto?.FileName ?? "";
        settings.ShowAllFolders     = _vm.OrganiseVM.ShowAllFolders;
        settings.ShowAllPhotos      = _vm.OrganiseVM.ShowAllPhotos;
        settings.Save();
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
            _organiseWindowState = WindowState;
            MainToolbar.Visibility = Visibility.Collapsed;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            Dispatcher.InvokeAsync(Focus, DispatcherPriority.Input);
        }
        else
        {
            WindowStyle = WindowStyle.SingleBorderWindow;
            WindowState = _organiseWindowState;
            MainToolbar.Visibility = Visibility.Visible;
        }
    }

    private void About_Click(object sender, RoutedEventArgs e) =>
        new AboutWindow { Owner = this }.ShowDialog();

    private void Undo_Click(object sender, RoutedEventArgs e) =>
        _vm.OrganiseVM.Undo();

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Z
            && (e.KeyboardDevice.Modifiers & ModifierKeys.Control) != 0
            && _vm.CurrentMode == AppMode.Organise)
        {
            _vm.OrganiseVM.Undo();
            e.Handled = true;
            return;
        }

        if (_vm.CurrentMode != AppMode.Present) return;

        var pvm = _vm.PresentVM;
        switch (e.Key)
        {
            case Key.Right:
                pvm.NextPhoto();
                e.Handled = true;
                break;
            case Key.Space:
                if (pvm.CurrentIsVideo)
                    pvm.IsPlaying = !pvm.IsPlaying;
                else
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
