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
            services.AddSqliteSecurityGrantStore(target, (SqliteSecurityGrantStoreSettings) null!));

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

    /// <summary>Verifies more than one ambiguous unkeyed captured registration is rejected rather than arbitrarily chosen.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenMultipleUnkeyedTargetsAreAlreadyRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(CreateTarget(Path.Combine(Path.GetTempPath(), "first-grants.db")));
        _ = services.AddSingleton(CreateTarget(Path.Combine(Path.GetTempPath(), "second-grants.db")));

        var exception = Should.Throw<InvalidOperationException>(() => services.AddSqliteSecurityGrantStore(
            CreateTarget(Path.Combine(Path.GetTempPath(), "third-grants.db")),
            SqliteSecurityGrantStoreSettings.CreateDefault()));

        exception.Message.ShouldContain(nameof(SqliteSecurityGrantStoreTarget));
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

    /// <summary>Verifies the configure delegate's mutations become the captured immutable settings singleton.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenConfigureIsProvided_CapturesConfiguredSettings()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var services = new ServiceCollection();

        _ = services.AddSqliteSecurityGrantStore(target, static options =>
        {
            options.LockTimeout = TimeSpan.FromSeconds(9);
            options.MaximumClaims = 7;
        });
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var settings = provider.GetRequiredService<SqliteSecurityGrantStoreSettings>();

        settings.LockTimeout.ShouldBe(TimeSpan.FromSeconds(9));
        settings.MaximumClaims.ShouldBe(7);
        settings.MaximumGrantBytes.ShouldBe(1_048_576);
        settings.MaximumEnforcementBytes.ShouldBe(1_048_576);
        settings.MaximumResources.ShouldBe(256);
        settings.MaximumDelegationLinks.ShouldBe(32);
        provider.GetRequiredService<SqliteSecurityGrantStoreTarget>().ShouldBe(target);
        _ = provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<SqliteSecurityGrantStore>();
        File.Exists(target.DatabasePath).ShouldBeFalse();
    }

    /// <summary>Verifies omitting the delegate binds to the configure overload and captures the documented defaults.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenConfigureIsOmitted_CapturesDefaultSettings()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var services = new ServiceCollection();

        _ = services.AddSqliteSecurityGrantStore(target);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<SqliteSecurityGrantStoreSettings>()
            .ShouldBe(SqliteSecurityGrantStoreSettings.CreateDefault());
        _ = provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<SqliteSecurityGrantStore>();
    }

    /// <summary>Verifies invalid configured bounds fail eagerly at registration before any descriptor is added.</summary>
    [Theory]
    [InlineData("lockTimeout")]
    [InlineData("maximumGrantBytes")]
    [InlineData("maximumEnforcementBytes")]
    [InlineData("maximumResources")]
    [InlineData("maximumClaims")]
    [InlineData("maximumDelegationLinks")]
    public void AddSqliteSecurityGrantStore_WhenConfiguredValueIsInvalid_ThrowsBeforeMutation(string paramName)
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddSqliteSecurityGrantStore(target, options =>
            {
                switch (paramName)
                {
                    case "lockTimeout":
                        options.LockTimeout = TimeSpan.FromMilliseconds(1500);
                        break;
                    case "maximumGrantBytes":
                        options.MaximumGrantBytes = 0;
                        break;
                    case "maximumEnforcementBytes":
                        options.MaximumEnforcementBytes = -1;
                        break;
                    case "maximumResources":
                        options.MaximumResources = 0;
                        break;
                    case "maximumClaims":
                        options.MaximumClaims = 0;
                        break;
                    default:
                        options.MaximumDelegationLinks = 0;
                        break;
                }
            }));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(paramName);
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies the configure overload rejects missing inputs with the exact parameter and never runs the delegate.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenConfigureOverloadArgumentIsNull_ThrowsExactArgumentWithoutInvokingDelegate()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var services = new ServiceCollection();
        var invoked = false;
        void Configure(SqliteSecurityGrantStoreOptions options) => invoked = true;

        var nullServices = Should.Throw<ArgumentNullException>(() =>
            ServiceExtensions.AddSqliteSecurityGrantStore(null!, target, Configure));
        var nullTarget = Should.Throw<ArgumentNullException>(() =>
            services.AddSqliteSecurityGrantStore(null!, Configure));

        nullServices.ParamName.ShouldBe("services");
        nullTarget.ParamName.ShouldBe("target");
        invoked.ShouldBeFalse();
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies repeating the same configure delegate captures one settings singleton and one store selection.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenSameConfigureRepeats_IsIdempotent()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var services = new ServiceCollection();
        static void Configure(SqliteSecurityGrantStoreOptions options) => options.MaximumResources = 11;

        _ = services.AddSqliteSecurityGrantStore(target, Configure);
        var count = services.Count;
        _ = services.AddSqliteSecurityGrantStore(target, Configure);

        services.Count.ShouldBe(count);
        services.Count(static descriptor =>
            descriptor.ServiceType == typeof(SqliteSecurityGrantStoreSettings)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore)).ShouldBe(1);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<SqliteSecurityGrantStoreSettings>().MaximumResources.ShouldBe(11);
    }

    /// <summary>Verifies a repeat with different effective configured settings is rejected before collection mutation.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenDifferentConfigureRepeats_RejectsBeforeMutation()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var services = new ServiceCollection();
        _ = services.AddSqliteSecurityGrantStore(target, static options => options.MaximumClaims = 7);
        var count = services.Count;

        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddSqliteSecurityGrantStore(target, static options => options.MaximumClaims = 8));

        services.Count.ShouldBe(count);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<SqliteSecurityGrantStoreSettings>().MaximumClaims.ShouldBe(7);
    }

    /// <summary>Verifies equal effective settings are idempotent across the settings and configure overloads in either order.</summary>
    [Fact]
    public void AddSqliteSecurityGrantStore_WhenOverloadsProduceEqualSettings_IsIdempotentAcrossOverloads()
    {
        var target = CreateTarget(Path.Combine(Path.GetTempPath(), "grants.db"));
        var settings = new SqliteSecurityGrantStoreSettings(TimeSpan.FromSeconds(3), 100, 200, 10, 20, 5);
        static void Configure(SqliteSecurityGrantStoreOptions options)
        {
            options.LockTimeout = TimeSpan.FromSeconds(3);
            options.MaximumGrantBytes = 100;
            options.MaximumEnforcementBytes = 200;
            options.MaximumResources = 10;
            options.MaximumClaims = 20;
            options.MaximumDelegationLinks = 5;
        }

        var settingsFirst = new ServiceCollection();
        _ = settingsFirst.AddSqliteSecurityGrantStore(target, settings);
        var settingsFirstCount = settingsFirst.Count;
        _ = Should.NotThrow(() => settingsFirst.AddSqliteSecurityGrantStore(target, Configure));

        var configureFirst = new ServiceCollection();
        _ = configureFirst.AddSqliteSecurityGrantStore(target, Configure);
        var configureFirstCount = configureFirst.Count;
        _ = Should.NotThrow(() => configureFirst.AddSqliteSecurityGrantStore(target, settings));

        settingsFirst.Count.ShouldBe(settingsFirstCount);
        configureFirst.Count.ShouldBe(configureFirstCount);
        using var provider = configureFirst.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<SqliteSecurityGrantStoreSettings>().ShouldBe(settings);
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
        public ValueTask<GrantRevocationResult> RevokeAsync(GrantId grantId, RevocationReason reason, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(reason);
            return ValueTask.FromResult<GrantRevocationResult>(new GrantRevocationNotFound(grantId));
        }
    }
}
