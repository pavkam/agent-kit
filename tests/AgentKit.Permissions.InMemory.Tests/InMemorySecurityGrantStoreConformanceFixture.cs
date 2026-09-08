// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory.Tests;

using AgentKit.Conformance;

/// <summary>Composes the first-party in-memory grant store through its public service-registration surface.</summary>
public sealed class InMemorySecurityGrantStoreConformanceFixture: ISecurityGrantStoreConformanceFixture
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero));
    private ServiceProvider? _services;

    /// <inheritdoc/>
    public TimeProvider TimeProvider => _timeProvider;

    /// <inheritdoc/>
    public ValueTask<ISecurityGrantStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _services ??= CreateServices();
        return ValueTask.FromResult(_services.GetRequiredService<ISecurityGrantStore>());
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
        _services?.Dispose();
        return ValueTask.CompletedTask;
    }

    private ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(_timeProvider);
        _ = services.AddInMemorySecurityGrantStore();
        return services.BuildServiceProvider(validateScopes: true);
    }
}
