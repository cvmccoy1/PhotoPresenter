using System.Windows;
using PhotoPresenter.ViewModels;

namespace PhotoPresenter.Tests.Unit;

/// <summary>
/// Tests that verify MainWindow responds correctly to ViewModel mode changes.
/// All tests run on the shared WPF STA Dispatcher thread (via WpfApplicationFixture).
///
/// Windows are created but not shown (no Show() call), so tests never paint to the screen
/// and do not call Window.Close() — which would trigger Window_Closing and overwrite user
/// settings. The windows are left for GC; FileSystemWatchers will be collected with them.
/// </summary>
[Collection("WPF")]
public class MainWindowTests
{
    private readonly WpfApplicationFixture _fixture;

    public MainWindowTests(WpfApplicationFixture fixture) => _fixture = fixture;

    // ── Initial state ─────────────────────────────────────────────────────────

    [Fact]
    public void InitialMode_IsOrganise_WithBorderedWindow()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;

            Assert.Equal(AppMode.Organise, vm.CurrentMode);
            Assert.Equal(WindowStyle.SingleBorderWindow, window.WindowStyle);
        });
    }

    [Fact]
    public void InitialCurrentView_IsOrganiseViewModel()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;

            Assert.IsType<OrganiseViewModel>(vm.CurrentView);
        });
    }

    // ── ApplyMode — Present ───────────────────────────────────────────────────

    [Fact]
    public void CurrentMode_SwitchedToPresent_SetsWindowStyleNone()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;

            vm.CurrentMode = AppMode.Present;

            Assert.Equal(WindowStyle.None, window.WindowStyle);
        });
    }

    [Fact]
    public void CurrentMode_SwitchedToPresent_MaximizesWindow()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;

            vm.CurrentMode = AppMode.Present;

            Assert.Equal(WindowState.Maximized, window.WindowState);
        });
    }

    [Fact]
    public void CurrentMode_SwitchedToPresent_CurrentViewIsPresentViewModel()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;

            vm.CurrentMode = AppMode.Present;

            Assert.IsType<PresentViewModel>(vm.CurrentView);
        });
    }

    // ── ApplyMode — back to Organise ──────────────────────────────────────────

    [Fact]
    public void CurrentMode_SwitchedBackToOrganise_RestoresBorderedWindowStyle()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.CurrentMode = AppMode.Present;

            vm.CurrentMode = AppMode.Organise;

            Assert.Equal(WindowStyle.SingleBorderWindow, window.WindowStyle);
        });
    }

    [Fact]
    public void CurrentMode_SwitchedBackToOrganise_CurrentViewIsOrganiseViewModel()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.CurrentMode = AppMode.Present;

            vm.CurrentMode = AppMode.Organise;

            Assert.IsType<OrganiseViewModel>(vm.CurrentView);
        });
    }

    // ── SwitchToOrganise (VM method) ──────────────────────────────────────────

    [Fact]
    public void SwitchToOrganise_AfterPresentMode_ReturnsModeToOrganise()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.CurrentMode = AppMode.Present;

            vm.SwitchToOrganise();

            Assert.Equal(AppMode.Organise, vm.CurrentMode);
        });
    }

    [Fact]
    public void SwitchToOrganise_AfterPresentMode_RestoresWindowStyle()
    {
        _fixture.Invoke(() =>
        {
            var window = new MainWindow();
            var vm = (MainViewModel)window.DataContext;
            vm.CurrentMode = AppMode.Present;

            vm.SwitchToOrganise();

            Assert.Equal(WindowStyle.SingleBorderWindow, window.WindowStyle);
        });
    }
}
