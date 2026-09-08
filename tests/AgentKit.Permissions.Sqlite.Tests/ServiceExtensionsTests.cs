// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Sqlite.Tests;

/// <summary>Verifies explicit, order-independent SQLite adapter selection evidence.</summary>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies exact repeats share one selected singleton and do not initialize the database.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenRepeatedExactly_RegistersOneUninitializedSingleton()
    {
        var directory = TestTemporaryDirectory.Create();
        try
        {
            var target = CreateTarget(Path.Combine(directory, "grants.db"));
            var settings = SqliteSecurityGrantStoreSettings.CreateDefault();
            var services = new ServiceCollection();

            _ = services.AddSqliteSecurityGrantStore(target, settings);
            _ = services.AddSqliteSecurityGrantStore(target, settings);
            using var provider = services.BuildServiceProvider(validateScopes: true);
            var stores = provider.GetServices<ISecurityGrantStore>().ToArray();

            _ = stores.ShouldHaveSingleItem().ShouldBeOfType<SqliteSecurityGrantStore>();
            provider.GetRequiredService<ISecurityGrantStore>().ShouldBeSameAs(stores[0]);
            File.Exists(target.DatabasePath).ShouldBeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Verifies conflicting repeated leaf configuration fails before collection mutation.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenRepeatConflicts_RejectsBeforeMutation()
    {
        var first = CreateTarget(Path.Combine(Path.GetTempPath(), "first-grants.db"));
        var second = CreateTarget(Path.Combine(Path.GetTempPath(), "second-grants.db"));
        var services = new ServiceCollection();
        _ = services.AddSqliteSecurityGrantStore(first, SqliteSecurityGrantStoreSettings.CreateDefault());
        var count = services.Count;

        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddSqliteSecurityGrantStore(second, SqliteSecurityGrantStoreSettings.CreateDefault()));

        services.Count.ShouldBe(count);

        var differentSettings = new SqliteSecurityGrantStoreSettings(
            TimeSpan.FromSeconds(2), 1, 1, 1, 1, 1);
        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddSqliteSecurityGrantStore(first, differentSettings));
        services.Count.ShouldBe(count);
    }

    /// <summary>Verifies every missing registration input is rejected with its exact parameter before mutation.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenArgumentIsNull_ThrowsExactArgument()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var settings = SqliteSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        var nullServices = Should.Throw<ArgumentNullException>(() =>
            ServiceExtensions.AddSqliteSecurityGrantStore(null!, target, settings));
        var nullTarget = Should.Throw<ArgumentNullException>(() =>
            services.AddSqliteSecurityGrantStore(null!, settings));
        var nullSettings = Should.Throw<ArgumentNullException>(() =>
            services.AddSqliteSecurityGrantStore(target, null!));

        nullServices.ParamName.ShouldBe("services");
        nullTarget.ParamName.ShouldBe("target");
        nullSettings.ParamName.ShouldBe("settings");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies custom registrations remain visible rather than becoming registration-order selection.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenCustomStoreExists_PreservesVisibleAmbiguity()
    {
        var services = new ServiceCollection();
        var custom = new FixedStore();
        _ = services.AddSingleton<ISecurityGrantStore>(custom);
        _ = services.AddSqliteSecurityGrantStore(
            CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db")),
            SqliteSecurityGrantStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider();

        var stores = provider.GetServices<ISecurityGrantStore>().ToArray();
        stores.Length.ShouldBe(2);
        stores.ShouldContain(custom);
        stores.ShouldContain(static store => store is SqliteSecurityGrantStore);
    }

    /// <summary>Verifies an existing same-concrete instance cannot suppress the leaf's captured singleton descriptor.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenSameConcreteInstanceExists_PreservesBothSelections()
    {
        var firstTarget = CreateTarget(Path.Combine(Path.GetTempPath(), "first-grants.db"));
        var secondTarget = CreateTarget(Path.Combine(Path.GetTempPath(), "second-grants.db"));
        var existing = new SqliteSecurityGrantStore(
            firstTarget, SqliteSecurityGrantStoreSettings.CreateDefault(), TimeProvider.System);
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(existing);

        _ = services.AddSqliteSecurityGrantStore(secondTarget, SqliteSecurityGrantStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var stores = provider.GetServices<ISecurityGrantStore>().ToArray();

        stores.Length.ShouldBe(2);
        stores.ShouldContain(existing);
        stores.Count(static store => store is SqliteSecurityGrantStore).ShouldBe(2);
    }

    /// <summary>Verifies a different-lifetime same-concrete descriptor remains visible beside the selected singleton.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenScopedConcreteDescriptorExists_PreservesBothDescriptors()
    {
        var services = new ServiceCollection();
        _ = services.AddScoped<ISecurityGrantStore, SqliteSecurityGrantStore>();

        _ = services.AddSqliteSecurityGrantStore(
            CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db")),
            SqliteSecurityGrantStoreSettings.CreateDefault());

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore)).ShouldBe(2);
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore)
            && descriptor.Lifetime == ServiceLifetime.Singleton
            && descriptor.ImplementationType == typeof(SqliteSecurityGrantStore));
    }

    /// <summary>Verifies unrelated non-null keyed leaf configuration does not alter the unkeyed captured target.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenOtherKeyHasConfiguration_IgnoresKeyedEvidence()
    {
        var expectedTarget = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var expectedSettings = SqliteSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton(
            "other", CreateTarget(Path.Combine(Path.GetTempPath(), "other-grants.db")));
        _ = services.AddKeyedSingleton(
            "other", new SqliteSecurityGrantStoreSettings(TimeSpan.FromSeconds(2), 1, 1, 1, 1, 1));

        _ = services.AddSqliteSecurityGrantStore(expectedTarget, expectedSettings);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<SqliteSecurityGrantStoreTarget>().ShouldBe(expectedTarget);
        provider.GetRequiredService<SqliteSecurityGrantStoreSettings>().ShouldBe(expectedSettings);
        provider.GetRequiredKeyedService<SqliteSecurityGrantStoreTarget>("other").ShouldNotBe(expectedTarget);
        _ = provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<SqliteSecurityGrantStore>();
    }

    /// <summary>Verifies null-keyed captured values participate in the same unkeyed DI selection semantics.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenNullKeyHasConfiguration_ReusesCapturedEvidence()
    {
        var expectedTarget = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var expectedSettings = SqliteSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<SqliteSecurityGrantStoreTarget>(null, expectedTarget);
        _ = services.AddKeyedSingleton<SqliteSecurityGrantStoreSettings>(null, expectedSettings);

        _ = services.AddSqliteSecurityGrantStore(expectedTarget, expectedSettings);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<SqliteSecurityGrantStoreTarget>().ShouldBe(expectedTarget);
        provider.GetRequiredService<SqliteSecurityGrantStoreSettings>().ShouldBe(expectedSettings);
        _ = provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<SqliteSecurityGrantStore>();
        services.Count(static descriptor =>
            descriptor.ServiceType == typeof(SqliteSecurityGrantStoreTarget)).ShouldBe(1);
        services.Count(static descriptor =>
            descriptor.ServiceType == typeof(SqliteSecurityGrantStoreSettings)).ShouldBe(1);
    }

    private static SqliteSecurityGrantStoreTarget CreateTarget(string path) => new(
        path,
        new SqliteSecurityGrantStoreInstanceId(Guid.Parse("10000000-0000-0000-0000-000000000001")),
        SqliteDatabaseOpenMode.CreateIfMissing,
        SqliteSchemaMode.ApplyKnownMigrations);

    private sealed class FixedStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "No effect."));
        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) => ValueTask.FromResult(false);
    }
}
