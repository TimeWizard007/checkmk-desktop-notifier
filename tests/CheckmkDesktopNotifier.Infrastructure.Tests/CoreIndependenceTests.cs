using CheckmkDesktopNotifier.Core.Abstractions;
using CheckmkDesktopNotifier.Core.Domain;
using CheckmkDesktopNotifier.Infrastructure.Rest;
using CheckmkDesktopNotifier.Infrastructure.Tests.TestSupport;

namespace CheckmkDesktopNotifier.Infrastructure.Tests;

public sealed class CoreIndependenceTests
{
    [Fact]
    public void Core_assembly_does_not_reference_infrastructure()
    {
        var core = typeof(MonitoredProblem).Assembly;
        var names = core.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain("CheckmkDesktopNotifier.Infrastructure", names);
        Assert.DoesNotContain("System.Net.Http", names);
    }

    [Fact]
    public void Checkmk_client_port_is_defined_in_core()
    {
        Assert.Equal("CheckmkDesktopNotifier.Core", typeof(ICheckmkClient).Assembly.GetName().Name);
        Assert.Equal("CheckmkDesktopNotifier.Infrastructure", typeof(CheckmkRestClient).Assembly.GetName().Name);
        Assert.Contains(typeof(ICheckmkClient), typeof(CheckmkRestClient).GetInterfaces());
        Assert.Contains(typeof(ICheckmkClient), typeof(CheckmkServiceClient).GetInterfaces());
    }

    [Fact]
    public void Mapped_problems_are_core_types()
    {
        var problems = ServiceProblemMapper.MapCollection(
            FixtureReader.Read("service-collection.json"),
            TestOptions.Site);

        Assert.All(problems, problem =>
        {
            Assert.Same(typeof(MonitoredProblem).Assembly, problem.GetType().Assembly);
            Assert.IsType<MonitoredObjectId>(problem.Id);
            Assert.IsType<Severity>(problem.Severity);
            Assert.IsType<StateType>(problem.StateType);
        });
    }
}
