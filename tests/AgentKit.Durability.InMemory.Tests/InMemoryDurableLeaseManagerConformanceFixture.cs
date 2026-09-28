// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory.Tests;

/// <summary>Runs the reusable lease-manager contract suite against the process-local in-memory adapter.</summary>
/// <remarks>
/// The manager reads the fixture's controllable clock, so expiry and takeover are deterministic. Mutual exclusion
/// holds only within this process, which is exactly the scope the suite asserts.
/// </remarks>
public sealed class InMemoryDurableLeaseManagerConformanceFixture: IDurableLeaseManagerConformanceFixture
{
    private readonly FakeTimeProvider _clock = new(DurabilityConformanceData.Now);
    private readonly InMemoryDurableLeaseManager _manager;

    /// <summary>Initializes a manager owning no leases, driven by this fixture's clock.</summary>
    public InMemoryDurableLeaseManagerConformanceFixture() =>
        _manager = new InMemoryDurableLeaseManager(new GuidExecutionLeaseIdGenerator(), _clock);

    /// <inheritdoc/>
    public IDurableLeaseManager CreateLeaseManager() => _manager;

    /// <inheritdoc/>
    public void Advance(TimeSpan delta) => _clock.Advance(delta);
}
