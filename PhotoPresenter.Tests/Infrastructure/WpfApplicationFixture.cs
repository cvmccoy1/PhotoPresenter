using System.Windows;
using System.Windows.Threading;

namespace PhotoPresenter.Tests.Infrastructure;

/// <summary>
/// Provides a shared WPF Application and STA Dispatcher for tests that create windows or controls.
/// WPF requires (a) Application.Current to be non-null and (b) all UI objects to be created and
/// accessed on the same STA dispatcher thread. This fixture spins up a dedicated background STA
/// thread that runs a Dispatcher message loop and keeps it alive for the duration of the test
/// collection; the Application is created once per AppDomain.
///
/// Register via [CollectionDefinition("WPF")] + ICollectionFixture&lt;WpfApplicationFixture&gt;
/// and mark test classes with [Collection("WPF")] to share this instance.
/// </summary>
public sealed class WpfApplicationFixture : IDisposable
{
    public Dispatcher Dispatcher { get; }

    public WpfApplicationFixture()
    {
        var ready = new ManualResetEventSlim(false);
        Dispatcher? dispatcher = null;

        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run(); // blocks until InvokeShutdown()
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Name = "WPF-Test-Dispatcher";
        thread.Start();

        ready.Wait();
        Dispatcher = dispatcher!;
    }

    /// <summary>Marshals an action onto the WPF STA thread and blocks until it completes.</summary>
    public void Invoke(Action action) => Dispatcher.Invoke(action);

    /// <summary>Marshals a function onto the WPF STA thread, blocks, and returns its result.</summary>
    public T Invoke<T>(Func<T> func) => Dispatcher.Invoke(func);

    public void Dispose() => Dispatcher.InvokeShutdown();
}

/// <summary>Registers the "WPF" xUnit collection so all classes tagged [Collection("WPF")] share one fixture.</summary>
[CollectionDefinition("WPF")]
public class WpfCollection : ICollectionFixture<WpfApplicationFixture> { }
