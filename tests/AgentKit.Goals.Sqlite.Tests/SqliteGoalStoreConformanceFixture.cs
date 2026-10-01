// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Goals.Sqlite.Tests;

/// <summary>Composes a <see cref="SqliteGoalStore"/> over a temporary database for the shared goal-store suite.</summary>
public sealed class SqliteGoalStoreConformanceFixture: IGoalStoreConformanceFixture, IDisposable
{
    private static readonly ComponentId _scanner = new("test-worker");
    private readonly SqliteGoalTestDatabase _database = new();

    /// <summary>Initializes the isolated composition.</summary>
    public SqliteGoalStoreConformanceFixture() => Store = Open();

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new(supportsDurability: true);

    /// <inheritdoc/>
    public bool PreservesDelegation => true;

    /// <inheritdoc/>
    public ComponentId? IntentScanner => _scanner;

    /// <inheritdoc/>
    public TestGoalGrants Grants { get; } = new();

    /// <inheritdoc/>
    public IGoalStore Store { get; private set; }

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
        Store = Open();
        return ValueTask.FromResult(Store);
    }

    /// <inheritdoc/>
    public void Dispose() => _database.Dispose();

    private SqliteGoalStore Open() => new(
        _database.Target(),
        new SqliteGoalStoreSettings(TimeSpan.FromSeconds(5), 1_048_576, [_scanner]),
        Grants,
        new Ids(),
        TimeProvider.System);

    private sealed class Ids: IIdentifierGenerator<SecurityEnforcementIntentId>
    {
        public SecurityEnforcementIntentId Create() => new(Guid.NewGuid());
    }
}
