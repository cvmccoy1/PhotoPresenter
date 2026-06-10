using Xunit.Abstractions;
using Xunit.Sdk;

namespace PhotoPresenter.Tests.Infrastructure;

[XunitTestCaseDiscoverer("PhotoPresenter.Tests.Infrastructure.StaTheoryDiscoverer", "PhotoPresenter.Tests")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class StaTheoryAttribute : TheoryAttribute { }

public sealed class StaTheoryDiscoverer : IXunitTestCaseDiscoverer
{
    private readonly IMessageSink _diagnosticMessageSink;

    public StaTheoryDiscoverer(IMessageSink diagnosticMessageSink)
    {
        _diagnosticMessageSink = diagnosticMessageSink;
    }

    public IEnumerable<IXunitTestCase> Discover(
        ITestFrameworkDiscoveryOptions discoveryOptions,
        ITestMethod testMethod,
        IAttributeInfo factAttribute)
    {
        var defaultDiscoverer = new TheoryDiscoverer(_diagnosticMessageSink);
        foreach (var testCase in defaultDiscoverer.Discover(discoveryOptions, testMethod, factAttribute))
            yield return new StaTestCase(_diagnosticMessageSink,
                discoveryOptions.MethodDisplayOrDefault(),
                discoveryOptions.MethodDisplayOptionsOrDefault(),
                testMethod,
                testCase.TestMethodArguments);
    }
}
