// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Composes an audit dispatcher with deterministic time and fixture-owned additive sinks for one conformance case.</summary>
/// <remarks>
/// The fixture owns composition and disposal of every dispatcher it creates. It must pass the supplied delivery policy,
/// finite timeout, and exact sink bindings through the implementation's supported composition surface. Advancing time
/// affects every timeout scheduled by the fixture and never uses a wall clock.
/// </remarks>
public interface ISecurityAuditDispatcherConformanceFixture: IAsyncDisposable
{
    /// <summary>Gets the deterministic clock supplied to every dispatcher composed by this fixture.</summary>
    public TimeProvider TimeProvider { get; }

    /// <summary>Composes the public audit dispatcher under test for one bounded delivery scenario.</summary>
    /// <param name="delivery">The process-level audit delivery requirement for the scenario.</param>
    /// <param name="deliveryTimeout">The positive per-sink deadline supplied to the dispatcher.</param>
    /// <param name="sinks">The initialized additive sink declarations and instances to compose.</param>
    /// <param name="cancellationToken">Cancels fixture composition before a dispatcher is returned.</param>
    /// <returns>The composed public dispatcher for the exact scenario.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before composition completes.</exception>
    public ValueTask<ISecurityAuditDispatcher> CreateAsync(
        SecurityAuditDelivery delivery,
        TimeSpan deliveryTimeout,
        ImmutableArray<SecurityAuditDispatcherConformanceSink> sinks,
        CancellationToken cancellationToken = default);

    /// <summary>Advances the deterministic clock observed by every dispatcher composed through this fixture.</summary>
    /// <param name="duration">The positive simulated duration to elapse.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="duration"/> is not positive.</exception>
    public void Advance(TimeSpan duration);
}
