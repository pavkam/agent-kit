// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Json.Tests;

/// <summary>Composes a <see cref="JsonGoalStore"/> over a temporary root for the shared goal-store suite.</summary>
public sealed class JsonGoalStoreConformanceFixture: IGoalStoreConformanceFixture, IDisposable
{
    private static readonly ComponentId _scanner = new("test-worker");
    private readonly JsonGoalStoreTestRoot _root = new();
    private JsonGoalStore _store;

    /// <summary>Initializes the isolated composition.</summary>
    public JsonGoalStoreConformanceFixture() => _store = Open();

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public bool PreservesDelegation => true;

    /// <inheritdoc/>
    public ComponentId? IntentScanner => _scanner;

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IGoalStore Store => _store;

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
    public ValueTask<IGoalStore> ReopenAsync(CancellationToken cancellationToken = default)
    {
        _store.Dispose();
        _store = Open();
        return ValueTask.FromResult<IGoalStore>(_store);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _store.Dispose();
        _root.Dispose();
    }

    private JsonGoalStore Open() => new(
        _root.Target(),
        new JsonGoalStoreSettings(1_048_576, 1_048_576, 4_096, [_scanner], JsonEncodingSettings.CreateDefault()),
        Grants,
        new Ids(),
        TimeProvider.System);

    private sealed class Ids: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }
}
