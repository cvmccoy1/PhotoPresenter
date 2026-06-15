using System.Windows.Input;
using PhotoPresenter.Models;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Unit;

/// <summary>
/// Tests for MainWindow.HandleKeyDown — the extracted key-dispatch helper.
/// All tests run on the shared WPF STA Dispatcher thread (via WpfApplicationFixture).
/// Windows are created but not shown and are left for GC (no Close() call).
/// </summary>
[Collection("WPF")]
public class MainWindowKeyTests
{
    private readonly WpfApplicationFixture _fixture;

    public MainWindowKeyTests(WpfApplicationFixture fixture) => _fixture = fixture;

    private static List<PhotoFolderViewModel> MakeFolders(params int[] photoCounts)
    {
        var list = new List<PhotoFolderViewModel>();
        for (int fi = 0; fi < photoCounts.Length; fi++)
        {
            var folder = new PhotoFolder { Name = $"F{fi}", FullPath = $@"Z:\F{fi}" };
            for (int pi = 0; pi < photoCounts[fi]; pi++)
                folder.Photos.Add(new PhotoItem { FileName = $"p{pi}.jpg", FullPath = $@"Z:\F{fi}\p{pi}.jpg" });
            list.Add(new PhotoFolderViewModel(folder));
        }
        return list;
    }

    // ── Organise mode — keys that should NOT be consumed ─────────────────────────

    [Fact]
    public void HandleKeyDown_UnknownKey_InOrganiseMode_ReturnsFalse()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            Assert.False(window.HandleKeyDown(Key.A, ModifierKeys.None));
        });
    }

    [Fact]
    public void HandleKeyDown_ArrowKeys_InOrganiseMode_ReturnsFalse()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            Assert.False(window.HandleKeyDown(Key.Right, ModifierKeys.None));
            Assert.False(window.HandleKeyDown(Key.Left, ModifierKeys.None));
        });
    }

    // ── Organise mode — Ctrl+Z ────────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_CtrlZ_InOrganiseMode_ReturnsTrue()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            bool result = window.HandleKeyDown(Key.Z, ModifierKeys.Control);
            Assert.True(result);
        });
    }

    [Fact]
    public void HandleKeyDown_ZWithoutCtrl_InOrganiseMode_ReturnsFalse()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            Assert.False(window.HandleKeyDown(Key.Z, ModifierKeys.None));
        });
    }

    // ── Organise mode — F5 ───────────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_F5_InOrganiseMode_ReturnsTrue()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            // SwitchToPresentCommand is a no-op with empty folders; key is still consumed.
            bool result = window.HandleKeyDown(Key.F5, ModifierKeys.None);
            Assert.True(result);
        });
    }

    [Fact]
    public void HandleKeyDown_F5_InOrganiseMode_WithNoFolders_DoesNotChangeMode()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            window.HandleKeyDown(Key.F5, ModifierKeys.None);
            Assert.Equal(AppMode.Organise, vm.CurrentMode);
        });
    }

    // ── Organise mode — Space ─────────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_Space_InOrganiseMode_WithNoFocusedControl_ReturnsTrue()
    {
        _fixture.Invoke(() =>
        {
            // In an unshown window Keyboard.FocusedElement is null, which matches
            // "not (ButtonBase or ComboBox or TextBoxBase)" — Space is consumed.
            var window = new MainWindow();
            bool result = window.HandleKeyDown(Key.Space, ModifierKeys.None);
            Assert.True(result);
        });
    }

    // ── Present mode — navigation ─────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_Right_InPresentMode_CallsNextPhoto()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            var folders = MakeFolders(3);
            vm.PresentVM.SetFolders(folders, 0, 0);
            vm.CurrentMode = AppMode.Present;

            bool handled = window.HandleKeyDown(Key.Right, ModifierKeys.None);

            Assert.True(handled);
            Assert.Same(folders[0].Photos[1], vm.PresentVM.CurrentPhotoItem);
        });
    }

    [Fact]
    public void HandleKeyDown_Left_InPresentMode_CallsPreviousPhoto()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            var folders = MakeFolders(3);
            vm.PresentVM.SetFolders(folders, 0, 2);
            vm.CurrentMode = AppMode.Present;

            bool handled = window.HandleKeyDown(Key.Left, ModifierKeys.None);

            Assert.True(handled);
            Assert.Same(folders[0].Photos[1], vm.PresentVM.CurrentPhotoItem);
        });
    }

    [Fact]
    public void HandleKeyDown_Space_InPresentMode_OnPhoto_CallsNextPhoto()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            var folders = MakeFolders(3);
            vm.PresentVM.SetFolders(folders, 0, 0);
            vm.CurrentMode = AppMode.Present;

            bool handled = window.HandleKeyDown(Key.Space, ModifierKeys.None);

            Assert.True(handled);
            Assert.Same(folders[0].Photos[1], vm.PresentVM.CurrentPhotoItem);
        });
    }

    // ── Present mode — zoom ───────────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_Add_InPresentMode_ZoomsIn()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.PresentVM.SetFolders(MakeFolders(1));
            vm.CurrentMode = AppMode.Present;
            double before = vm.PresentVM.ZoomScale;

            bool handled = window.HandleKeyDown(Key.Add, ModifierKeys.None);

            Assert.True(handled);
            Assert.True(vm.PresentVM.ZoomScale > before);
        });
    }

    [Fact]
    public void HandleKeyDown_OemPlus_InPresentMode_ZoomsIn()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.PresentVM.SetFolders(MakeFolders(1));
            vm.CurrentMode = AppMode.Present;
            double before = vm.PresentVM.ZoomScale;

            bool handled = window.HandleKeyDown(Key.OemPlus, ModifierKeys.None);

            Assert.True(handled);
            Assert.True(vm.PresentVM.ZoomScale > before);
        });
    }

    [Fact]
    public void HandleKeyDown_Subtract_InPresentMode_ZoomsOut()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.PresentVM.SetFolders(MakeFolders(1));
            vm.CurrentMode = AppMode.Present;
            vm.PresentVM.ZoomIn();
            double before = vm.PresentVM.ZoomScale;

            bool handled = window.HandleKeyDown(Key.Subtract, ModifierKeys.None);

            Assert.True(handled);
            Assert.True(vm.PresentVM.ZoomScale < before);
        });
    }

    [Fact]
    public void HandleKeyDown_OemMinus_InPresentMode_ZoomsOut()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.PresentVM.SetFolders(MakeFolders(1));
            vm.CurrentMode = AppMode.Present;
            vm.PresentVM.ZoomIn();
            double before = vm.PresentVM.ZoomScale;

            bool handled = window.HandleKeyDown(Key.OemMinus, ModifierKeys.None);

            Assert.True(handled);
            Assert.True(vm.PresentVM.ZoomScale < before);
        });
    }

    // ── Present mode — Escape ─────────────────────────────────────────────────────

    [Fact]
    public void HandleKeyDown_Escape_InPresentMode_SwitchesToOrganise()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.CurrentMode = AppMode.Present;

            bool handled = window.HandleKeyDown(Key.Escape, ModifierKeys.None);

            Assert.True(handled);
            Assert.Equal(AppMode.Organise, vm.CurrentMode);
        });
    }

    [Fact]
    public void HandleKeyDown_UnknownKey_InPresentMode_ReturnsFalse()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.CurrentMode = AppMode.Present;

            Assert.False(window.HandleKeyDown(Key.A, ModifierKeys.None));
        });
    }
}
