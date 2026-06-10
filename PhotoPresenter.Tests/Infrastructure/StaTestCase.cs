using Xunit.Abstractions;
using Xunit.Sdk;

namespace PhotoPresenter.Tests.Infrastructure;

public sealed class StaTestCase : XunitTestCase
{
    [Obsolete("Called by the de-serializer; should not be called by anyone else.", error: false)]
    public StaTestCase() { }

    public StaTestCase(
        IMessageSink diagnosticMessageSink,
        TestMethodDisplay defaultMethodDisplay,
        TestMethodDisplayOptions defaultMethodDisplayOptions,
        ITestMethod testMethod,
        object?[]? testMethodArguments = null)
        : base(diagnosticMessageSink, defaultMethodDisplay, defaultMethodDisplayOptions, testMethod, testMethodArguments)
    { }

    public override Task<RunSummary> RunAsync(
        IMessageSink diagnosticMessageSink,
        IMessageBus messageBus,
        object[] constructorArguments,
        ExceptionAggregator aggregator,
        CancellationTokenSource cancellationTokenSource)
    {
        var tcs = new TaskCompletionSource<RunSummary>();
        var thread = new Thread(() =>
        {
            try
            {
                var result = base.RunAsync(
                    diagnosticMessageSink, messageBus, constructorArguments,
                    aggregator, cancellationTokenSource).GetAwaiter().GetResult();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return tcs.Task;
    }
}
