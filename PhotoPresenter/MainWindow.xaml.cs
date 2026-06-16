using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
        InitThemeComboBox();
        InitTextComboBox();
        ThemeComboBox.SelectionChanged += ThemeComboBox_SelectionChanged;
        TextComboBox.SelectionChanged  += TextComboBox_SelectionChanged;
    }

    private void InitThemeComboBox()
    {
        var theme = _settings.Theme ?? "Light";
        foreach (ComboBoxItem item in ThemeComboBox.Items)
        {
            if ((string)item.Tag == theme) { ThemeComboBox.SelectedItem = item; return; }
        }
        ThemeComboBox.SelectedIndex = 0;
    }

    private void InitTextComboBox()
    {
        var size = _settings.TextSize ?? "Normal";
        foreach (ComboBoxItem item in TextComboBox.Items)
        {
            if ((string)item.Tag == size) { TextComboBox.SelectedItem = item; return; }
        }
        TextComboBox.SelectedIndex = 1;
    }

    private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ThemeComboBox.SelectedItem is not ComboBoxItem item) return;
        var themeName = (string)item.Tag;
        ThemeService.ApplyColor(themeName);
        var settings = UserSettings.Load();
        settings.Theme = themeName;
        settings.Save();
    }

    private void TextComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TextComboBox.SelectedItem is not ComboBoxItem item) return;
        var textSize = (string)item.Tag;
        ThemeService.ApplyTextSize(textSize);
        var settings = UserSettings.Load();
        settings.TextSize = textSize;
        settings.Save();
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
        _vm.OrganiseVM.Dispose();
        ThumbnailCache.Shutdown();

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
        settings.Volume             = _vm.PresentVM.Volume;
        settings.FadeTransitionEnabled = _vm.PresentVM.IsFadeEnabled;
        if (ThemeComboBox.SelectedItem is ComboBoxItem themeItem)
            settings.Theme = (string)themeItem.Tag;
        if (TextComboBox.SelectedItem is ComboBoxItem textItem)
            settings.TextSize = (string)textItem.Tag;
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
        if (HandleKeyDown(e.Key, e.KeyboardDevice.Modifiers))
            e.Handled = true;
    }

    // Extracted for testability — returns true when the key was consumed.
    internal bool HandleKeyDown(Key key, ModifierKeys modifiers)
    {
        if (key == Key.Z
            && (modifiers & ModifierKeys.Control) != 0
            && _vm.CurrentMode == AppMode.Organise)
        {
            _vm.OrganiseVM.Undo();
            return true;
        }

        if (key == Key.F5 && _vm.CurrentMode == AppMode.Organise)
        {
            _vm.SwitchToPresentCommand.Execute(null);
            return true;
        }

        if (key == Key.Space && _vm.CurrentMode == AppMode.Organise)
        {
            // Only intercept Space when the focused element doesn't already use it
            // (buttons click, checkboxes toggle, dropdowns open — let those through).
            if (Keyboard.FocusedElement is not (ButtonBase or ComboBox or TextBoxBase))
            {
                _vm.SwitchToPresentCommand.Execute(null);
                return true;
            }
            return false;
        }

        if (_vm.CurrentMode != AppMode.Present) return false;

        var pvm = _vm.PresentVM;
        switch (key)
        {
            case Key.Right:
                pvm.NextPhoto();
                return true;
            case Key.Space:
                if (pvm.CurrentIsVideo)
                    pvm.IsPlaying = !pvm.IsPlaying;
                else
                    pvm.NextPhoto();
                return true;
            case Key.Left:
                pvm.PreviousPhoto();
                return true;
            case Key.Add:
            case Key.OemPlus:
                pvm.ZoomIn();
                return true;
            case Key.Subtract:
            case Key.OemMinus:
                pvm.ZoomOut();
                return true;
            case Key.Escape:
                _vm.SwitchToOrganise();
                return true;
            case Key.P:
                pvm.IsAutoplayEnabled = !pvm.IsAutoplayEnabled;
                return true;
        }
        return false;
    }
}
