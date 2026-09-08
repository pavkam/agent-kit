// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Tests;

using AgentKit.Conformance;

/// <summary>Composes the default audit dispatcher through public Permissions registrations for reusable conformance cases.</summary>
public sealed class DefaultSecurityAuditDispatcherConformanceFixture: ISecurityAuditDispatcherConformanceFixture
{
    private readonly FakeTimeProvider _timeProvider = new(DateTimeOffset.UnixEpoch);
    private readonly List<ServiceProvider> _providers = [];

    /// <inheritdoc/>
    public TimeProvider TimeProvider => _timeProvider;

    /// <inheritdoc/>
    public ValueTask<ISecurityAuditDispatcher> CreateAsync(
        SecurityAuditDelivery delivery,
        TimeSpan deliveryTimeout,
        ImmutableArray<SecurityAuditDispatcherConformanceSink> sinks,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(_timeProvider);
        _ = services.AddAgentPermissions(options =>
        {
            options.AuditDelivery = delivery;
            options.AuditDeliveryTimeout = deliveryTimeout;
        });
        foreach (var sink in sinks)
        {
            _ = services.AddSecurityAuditSink(sink.Registration, sink.Sink);
        }

        var provider = services.BuildServiceProvider(validateScopes: true);
        _providers.Add(provider);
        return ValueTask.FromResult(provider.GetRequiredService<ISecurityAuditDispatcher>());
    }

    /// <inheritdoc/>
    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        _timeProvider.Advance(duration);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            provider.Dispose();
        }

        return ValueTask.CompletedTask;
    }
}
