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
        using var provider = services.BuildServiceProvider();

        _ = provider.GetRequiredService<ISessionCoordinator>().ShouldBeOfType<DefaultSessionCoordinator>();
        _ = provider.GetRequiredService<ISessionRunCoordinator>().ShouldBeOfType<DefaultSessionRunCoordinator>();
    }

    [Fact]
    public void AddAgentSession_WhenCalledTwice_KeepsFirstRegistration()
    {
        var services = new ServiceCollection();

        _ = services.AddAgentSession();
        _ = services.AddAgentSession();
        _ = services.AddInMemorySessionStore();
        using var provider = services.BuildServiceProvider();

        // No duplicate-registration exception, and exactly one coordinator resolves.
        provider.GetServices<ISessionCoordinator>().Count().ShouldBe(1);
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

    [Fact]
    public void AddSessionStore_WhenCalled_RegistersProvidedStore()
    {
        var services = new ServiceCollection();

        _ = services.AddSessionStore<InMemorySessionStore>();
        _ = services.AddInMemorySessionStore();
        using var provider = services.BuildServiceProvider();

        // TryAdd keeps the first registration.
        provider.GetServices<ISessionStore>().Count().ShouldBe(1);
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
