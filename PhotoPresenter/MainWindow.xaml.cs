using System.ComponentModel;
using System.Windows;
using Microsoft.Win32;
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
        settings.AutoplayIntervalSeconds = _vm.PresentVM.AutoplayIntervalSeconds;
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

    private void ExportFavorites_Click(object sender, RoutedEventArgs e)
    {
        var favorites = _vm.OrganiseVM.GetAllFavorites();

        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var (destFolder, destShellItem) = Services.ShellFolderPicker.PickFolder(
            hwnd, "Select destination folder for exported favorites");

        if (destFolder == null && destShellItem == null) return; // cancelled

        if (destShellItem != null)
        {
            // MTP destination (phone) — skip delete-non-favorites (can't enumerate MTP easily).
            new ExportProgressDialog(favorites, destShellItem) { Owner = this }.ShowDialog();
            return;
        }

        // Normal filesystem destination — existing behaviour.
        var favoriteNames = new HashSet<string>(
            favorites.Select(f => f.FileName),
            StringComparer.OrdinalIgnoreCase);

        var toDelete = Directory.Exists(destFolder)
            ? Directory.GetFiles(destFolder!)
                .Select(f => Path.GetFileName(f)!)
                .Where(n => !favoriteNames.Contains(n))
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList()
            : [];

        List<string> deleteList = [];
        if (toDelete.Count > 0)
        {
            var confirm = new ExportDeleteConfirmDialog(toDelete) { Owner = this };
            confirm.ShowDialog();
            if (confirm.Choice == ExportDeleteChoice.Cancel) return;
            if (confirm.Choice == ExportDeleteChoice.DeleteAndExport)
                deleteList = toDelete;
        }

        new ExportProgressDialog(favorites, destFolder!, deleteList) { Owner = this }.ShowDialog();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) =>
        new SettingsWindow(_vm) { Owner = this }.ShowDialog();

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

        if (key == Key.OemQuestion)
        {
            new ShortcutsWindow { Owner = this }.ShowDialog();
            return true;
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
                {
                    if (pvm.IsVideoEnded)
                        pvm.NextPhoto();
                    else
                        pvm.IsPlaying = !pvm.IsPlaying;
                }
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
