// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Artifacts;


/// <summary>Verifies <see cref="ArtifactEventSinkRegistration"/> validation.</summary>
public sealed class ArtifactEventSinkRegistrationTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var registration = new ArtifactEventSinkRegistration(new ArtifactEventSinkId("audit"), 5, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped);
        registration.Id.ShouldBe(new ArtifactEventSinkId("audit"));
        registration.Order.ShouldBe(5);
        registration.Lifetime.ShouldBe(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped);
    }

    [Fact]
    public void Constructor_WhenIdIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new ArtifactEventSinkRegistration(default, 0, Microsoft.Extensions.DependencyInjection.ServiceLifetime.Singleton)).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenLifetimeIsUndefined_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactEventSinkRegistration(new ArtifactEventSinkId("audit"), 0, (Microsoft.Extensions.DependencyInjection.ServiceLifetime) 999)).ParamName.ShouldBe("lifetime");
}
