// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;

using Microsoft.Extensions.Options;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddAgentSession_WhenCalled_RegistersCoordinatorAndRunCoordinator()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentSession().AddInMemorySessionStore();

        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(ISessionCoordinator)
            && descriptor.ImplementationType == typeof(DefaultSessionCoordinator));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(ISessionRunCoordinator)
            && descriptor.ImplementationType == typeof(DefaultSessionRunCoordinator));
    }

    [Fact]
    public void AddAgentSession_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentSession();
        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionCoordinator)).ShouldBe(1);
    }

    [Fact]
    public void AddAgentSession_WhenConfigureProvided_AppliesOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentSession(o => o.MaximumAppendEntries = 5);
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<AgentSessionOptions>>().Value.MaximumAppendEntries.ShouldBe(5);
    }

    [Fact]
    public void AddAgentSession_WhenMaximumAppendEntriesIsNotPositive_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentSession(o => o.MaximumAppendEntries = 0);
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AgentSessionOptions>>().Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3601)]
    public void AddAgentSession_WhenSecurityRequestLifetimeIsOutsideBounds_FailsValidationOnAccess(int seconds)
    {
        var services = new ServiceCollection();
        _ = services.AddAgentSession(
            options => options.SecurityRequestLifetime = TimeSpan.FromSeconds(seconds));
        using var provider = services.BuildServiceProvider();

        _ = Should.Throw<OptionsValidationException>(
            () => provider.GetRequiredService<IOptions<AgentSessionOptions>>().Value);
    }

    [Fact]
    public void AddSessionStore_WhenCalled_RegistersProvidedStore()
    {
        var services = new ServiceCollection();

        _ = services.AddSessionStore<FakeSessionStore>();
        _ = services.AddSessionStore<FakeSessionStore>();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISessionStore>().Count().ShouldBe(2);
    }

    [Fact]
    public void AddSessionEventSink_WhenCalledMultipleTimes_RegistersAdditively()
    {
        var services = new ServiceCollection();

        _ = services.AddSessionEventSink<FakeSessionEventSink>();
        _ = services.AddSessionEventSink<FakeSessionEventSink>();
        using var provider = services.BuildServiceProvider();

        provider.GetServices<ISessionEventSink>().Count().ShouldBe(2);
    }

    [Fact]
    public void ReplaceSessionRetentionPolicy_WhenCalled_ReplacesDefault()
    {
        var services = new ServiceCollection();
        _ = services.AddAgentSession();

        _ = services.ReplaceSessionRetentionPolicy<AlwaysDeleteRetentionPolicy>();
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ISessionRetentionPolicy>().ShouldBeOfType<AlwaysDeleteRetentionPolicy>();
        provider.GetServices<ISessionRetentionPolicy>().Count().ShouldBe(1);
    }

    private sealed class AlwaysDeleteRetentionPolicy: ISessionRetentionPolicy
    {
        public ValueTask<SessionRetentionDecision> EvaluateAsync(
            SessionDescriptor session,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new SessionRetentionDecision(SessionRetentionAction.Delete, "always"));
    }
}
