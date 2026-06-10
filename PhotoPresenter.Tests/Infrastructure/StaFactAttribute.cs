using Xunit.Abstractions;
using Xunit.Sdk;

namespace PhotoPresenter.Tests.Infrastructure;

[XunitTestCaseDiscoverer("PhotoPresenter.Tests.Infrastructure.StaFactDiscoverer", "PhotoPresenter.Tests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class StaFactAttribute : FactAttribute { }

public sealed class StaFactDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public StaFactDiscoverer(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        yield return new StaTestCase(_diagnosticMessageSink,
            discoveryOptions.MethodDisplayOrDefault(),
            discoveryOptions.MethodDisplayOptionsOrDefault(),
            testMethod);
    }
}
