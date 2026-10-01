// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.InMemory.Tests;

/// <summary>Composes an <see cref="InMemoryGoalStore"/> over the test grant harness for the shared goal-store suite.</summary>
public sealed class InMemoryGoalStoreConformanceFixture: IGoalStoreConformanceFixture
{
    private static readonly ComponentId _scanner = new("test-worker");

    /// <summary>Initializes the isolated composition.</summary>
    public InMemoryGoalStoreConformanceFixture()
    {
        var options = new InMemoryGoalStoreOptions();
        options.AuthorizedIntentScanners.Add(_scanner);
        Store = new InMemoryGoalStore(Grants, new TestIntentIds(), TimeProvider.System, options);
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: false);

    /// <inheritdoc/>
    public bool PreservesDelegation => true;

    /// <inheritdoc/>
    public ComponentId? IntentScanner => _scanner;

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IGoalStore Store { get; }

    /// <inheritdoc/>
    public ValueTask<GoalConformanceOwner> CreateOwnerAsync(string tenant, CancellationToken cancellationToken = default)
    {
        var agent = GoalTestData.NewAgent();
        var session = GoalTestData.NewSession();
        var run = GoalTestData.NewRun();
        var identity = GoalTestData.Identity(tenant);
        return ValueTask.FromResult(new GoalConformanceOwner(agent, session, run, identity, GoalTestData.Authorization(agent, session, run, identity)));
    }

    /// <inheritdoc/>
    public ValueTask<IGoalStore> ReopenAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Store);

    private sealed class TestIntentIds: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }
}
