// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Sqlite.Tests;

/// <summary>Runs the reusable lease-manager contract suite against the host-local SQLite adapter.</summary>
/// <remarks>
/// The manager reads the fixture's controllable clock, so expiry and takeover are deterministic. Mutual exclusion
/// holds across processes sharing this database file, which is a superset of the scope the suite asserts.
/// </remarks>
public sealed class SqliteDurableLeaseManagerConformanceFixture: IDurableLeaseManagerConformanceFixture
{
    private readonly SqliteDurableTestStore _store = new();
    private readonly FakeTimeProvider _clock = new(DurabilityConformanceData.Now);
    private readonly SqliteDurableLeaseManager _manager;

    /// <summary>Initializes an empty durable store and one lease manager over it.</summary>
    public SqliteDurableLeaseManagerConformanceFixture()
    {
        _manager = new SqliteDurableLeaseManager(
            _store.CreateDatabase(), new ConformanceLeaseIdGenerator(), _clock);
        _manager.InitializeAsync().AsTask().GetAwaiter().GetResult();
    }

    /// <inheritdoc/>
    public IDurableLeaseManager CreateLeaseManager() => _manager;

    /// <inheritdoc/>
    public void Advance(TimeSpan delta) => _clock.Advance(delta);

    /// <summary>Produces distinct lease identities for the conformance manager.</summary>
    private sealed class ConformanceLeaseIdGenerator: IIdentifierGenerator<ExecutionLeaseId>
    {
        public ExecutionLeaseId Create() => new(Guid.NewGuid());
    }
}
