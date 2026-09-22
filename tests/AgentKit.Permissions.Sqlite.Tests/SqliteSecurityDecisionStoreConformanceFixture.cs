// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Creates one isolated initialized SQLite decision database for each reusable contract case.</summary>
public sealed class SqliteSecurityDecisionStoreConformanceFixture: ISecurityDecisionStoreConformanceFixture
{
    private readonly string _directory = TestTemporaryDirectory.Create();
    private readonly SqliteSecurityDecisionStoreTarget _target;
    private ServiceProvider? _provider;
    private SqliteSecurityDecisionStore? _store;

    /// <summary>Initializes a fixture with one fixed database target for the case lifetime.</summary>
    public SqliteSecurityDecisionStoreConformanceFixture()
    {
        _target = new SqliteSecurityDecisionStoreTarget(
            Path.Combine(_directory, "decisions.db"),
            new SqliteSecurityDecisionStoreInstanceId(Guid.NewGuid()),
            SqliteDatabaseOpenMode.CreateIfMissing,
            SqliteSchemaMode.ApplyKnownMigrations);
    }

    /// <inheritdoc/>
    public ConformanceCapabilities Capabilities { get; } = new();

    /// <inheritdoc/>
    public async ValueTask<ISecurityDecisionStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            var services = new ServiceCollection();
            _ = services.AddSqliteSecurityDecisionStore(_target, SqliteSecurityDecisionStoreSettings.CreateDefault());
            _provider = services.BuildServiceProvider(validateScopes: true);
            _store = _provider.GetRequiredService<ISecurityDecisionStore>().ShouldBeOfType<SqliteSecurityDecisionStore>();
            await _store.InitializeAsync(SecurityControlPlaneTestBootstrap.Create(), cancellationToken);
        }

        return _store;
    }

    /// <inheritdoc/>
    public IReadOnlyList<SecurityDecision> ReadRecorded(ISecurityDecisionStore store) =>
        ((SqliteSecurityDecisionStore) store).Decisions;

    /// <inheritdoc/>
    public async ValueTask<ISecurityDecisionStore> RecreateAsync(CancellationToken cancellationToken = default)
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
            _provider = null;
            _store = null;
        }

        return await CreateAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_provider is not null)
        {
            await _provider.DisposeAsync();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
