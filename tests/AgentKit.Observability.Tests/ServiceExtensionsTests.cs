// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Observability.Tests;



/// <summary>Verifies ServiceExtensions behavior and contracts.</summary>
public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentKitObservability_WhenCalledTwice_PreservesOneLoggingFoundation()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentKitObservability().AddAgentKitObservability();
        using var provider = services.BuildServiceProvider();
        _ = provider.GetRequiredService<ILoggerFactory>().ShouldNotBeNull();
        _ = provider.GetRequiredService<ILogger<ServiceExtensionsTests>>().ShouldNotBeNull();
    }

    [Fact]
    public void AddAgentKitObservability_WhenServicesNull_ThrowsArgumentNullException()
    {
        IServiceCollection services = null!;
        var exception = Should.Throw<ArgumentNullException>(services.AddAgentKitObservability);
        exception.ParamName.ShouldBe("services");
    }
}
