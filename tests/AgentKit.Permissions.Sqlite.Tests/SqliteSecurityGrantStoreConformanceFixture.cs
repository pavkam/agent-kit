// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

using AgentKit.Conformance;

/// <summary>Creates one isolated initialized SQLite database for each reusable grant-store contract case.</summary>
public sealed class SqliteSecurityGrantStoreConformanceFixture: ISecurityGrantStoreConformanceFixture
{
    private readonly FakeTimeProvider _timeProvider = new(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
    private readonly string _directory = TestTemporaryDirectory.Create();
    private ServiceProvider? _provider;
    private SqliteSecurityGrantStore? _store;

    /// <inheritdoc/>
    public TimeProvider TimeProvider => _timeProvider;

    /// <inheritdoc/>
    public async ValueTask<ISecurityGrantStore> CreateAsync(CancellationToken cancellationToken = default)
    {
        if (_store is null)
        {
            var services = new ServiceCollection();
            _ = services.AddSingleton<TimeProvider>(_timeProvider);
            _ = services.AddSqliteSecurityGrantStore(
                new SqliteSecurityGrantStoreTarget(
                    Path.Combine(_directory, "grants.db"),
                    new SqliteSecurityGrantStoreInstanceId(Guid.NewGuid()),
                    SqliteDatabaseOpenMode.CreateIfMissing,
                    SqliteSchemaMode.ApplyKnownMigrations),
                SqliteSecurityGrantStoreSettings.CreateDefault());
            _provider = services.BuildServiceProvider(validateScopes: true);
            _store = _provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<SqliteSecurityGrantStore>();
            await _store.InitializeAsync(cancellationToken);
        }
        return _store;
    }

    /// <inheritdoc/>
    public void Advance(TimeSpan duration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(duration, TimeSpan.Zero);
        _timeProvider.Advance(duration);
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
