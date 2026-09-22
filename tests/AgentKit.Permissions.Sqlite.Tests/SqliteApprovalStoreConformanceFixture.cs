// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Creates one isolated initialized SQLite approval database for each reusable contract case.</summary>
public sealed class SqliteApprovalStoreConformanceFixture: IApprovalStoreConformanceFixture
{
    private readonly string _directory = TestTemporaryDirectory.Create();
    private ServiceProvider? _provider;
    private SqliteApprovalStore? _store;

    /// <inheritdoc/>
    public async ValueTask<IApprovalStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            var services = new ServiceCollection();
            _ = services.AddSqliteApprovalStore(
                new SqliteApprovalStoreTarget(
                    Path.Combine(_directory, "approvals.db"),
                    new SqliteApprovalStoreInstanceId(Guid.NewGuid()),
                    SqliteDatabaseOpenMode.CreateIfMissing,
                    SqliteSchemaMode.ApplyKnownMigrations),
                SqliteApprovalStoreSettings.CreateDefault());
            _provider = services.BuildServiceProvider(validateScopes: true);
            _store = _provider.GetRequiredService<IApprovalStore>().ShouldBeOfType<SqliteApprovalStore>();
            await _store.InitializeAsync(SecurityControlPlaneTestBootstrap.Create(), cancellationToken);
        }

        return _store;
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
