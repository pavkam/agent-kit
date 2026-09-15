// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Sqlite.Tests;

using AgentKit.Session;
using AgentKit.Session.InMemory;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies explicit, additive SQLite session store composition and idempotent directory composition.</summary>
public sealed class ServiceExtensionsTests: IDisposable
{
    private static readonly ComponentId _audience = new("tests.session.directory");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"agentkit-session-di-{Guid.NewGuid():N}");

    public ServiceExtensionsTests() => Directory.CreateDirectory(_directory);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void AddSqliteSessionStore_WhenCalledTwice_RegistersOneSqliteStore()
    {
        // Repeating the SQLite registration is idempotent for the single "agentkit.sqlite" store key.
        var target = Target();
        var services = WithSecurityBoundaries();

        _ = services.AddSqliteSessionStore(target);
        _ = services.AddSqliteSessionStore(target);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        provider.GetServices<ISessionStore>().OfType<SqliteSessionStore>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenInMemoryStoreAlreadyRegistered_RegistersBothStores()
    {
        // Store registrations are additive: registration order never selects a store, the directory route does.
        var target = Target();
        var services = WithSecurityBoundaries();

        _ = services.AddInMemorySessionStore();
        _ = services.AddSqliteSessionStore(target);
        _ = services.AddInMemorySessionStore();
        _ = services.AddSqliteSessionStore(target);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var stores = provider.GetServices<ISessionStore>().ToArray();
        stores.Select(static store => store.GetType()).ShouldBe([typeof(InMemorySessionStore), typeof(SqliteSessionStore)]);
        new DefaultSessionStoreCatalog(stores).GetDescriptors().Select(static descriptor => descriptor.Key.Value)
            .ShouldBe(["agentkit.in-memory", "agentkit.sqlite"]);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenRegisteredBeforeInMemoryStore_RegistersBothStores()
    {
        var services = new ServiceCollection();

        _ = services.AddSqliteSessionStore(Target());
        _ = services.AddInMemorySessionStore();

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionStore)).ShouldBe(2);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenSettingsSupplied_CapturesThemWithoutPublishingSingletons()
    {
        // The store factory captures its target and settings; neither is published as an ambient singleton.
        var services = new ServiceCollection();

        _ = services.AddSqliteSessionStore(Target(), new SqliteSessionStoreSettings(TimeSpan.FromSeconds(2), 512, 8));

        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreSettings));
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreTarget));
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreOptions));
    }

    [Fact]
    public void AddSqliteSessionStore_WhenConfigureSupplied_InvokesDelegateOnceWithDefaultsAndRegistersStore()
    {
        var services = WithSecurityBoundaries();
        var invocations = 0;
        SqliteSessionStoreOptions? observed = null;

        var returned = services.AddSqliteSessionStore(Target(), options =>
        {
            invocations++;
            observed = options;
            options.MaximumIssuedReadSnapshots = 16;
        });

        returned.ShouldBeSameAs(services);
        invocations.ShouldBe(1);
        var seen = observed.ShouldNotBeNull();
        seen.LockTimeout.ShouldBe(SqliteSessionStoreSettings.CreateDefault().LockTimeout);
        seen.MaximumEntryPayloadBytes.ShouldBe(SqliteSessionStoreSettings.CreateDefault().MaximumEntryPayloadBytes);
        seen.MaximumIssuedReadSnapshots.ShouldBe(16);
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreOptions));
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreSettings));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetServices<ISessionStore>().ShouldHaveSingleItem().ShouldBeOfType<SqliteSessionStore>();
    }

    [Fact]
    public void AddSqliteSessionStore_WhenConfigureOverloadCalledTwice_RegistersOneSqliteStore()
    {
        var target = Target();
        var services = WithSecurityBoundaries();

        _ = services.AddSqliteSessionStore(target, static options => options.MaximumIssuedReadSnapshots = 2);
        _ = services.AddSqliteSessionStore(target, static options => options.MaximumIssuedReadSnapshots = 3);

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        provider.GetServices<ISessionStore>().OfType<SqliteSessionStore>().Count().ShouldBe(1);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenConfigureOverloadFollowsInMemoryStore_RegistersBothStores()
    {
        var services = new ServiceCollection();

        _ = services.AddInMemorySessionStore();
        _ = services.AddSqliteSessionStore(Target(), static _ => { });

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionStore)).ShouldBe(2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddSqliteSessionStore_WhenConfiguredPayloadBoundIsNotPositive_ThrowsWithoutRegistering(int bytes)
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(new object());
        var before = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddSqliteSessionStore(Target(), options => options.MaximumEntryPayloadBytes = bytes));

        exception.ParamName.ShouldBe("maximumEntryPayloadBytes");
        services.Count.ShouldBe(before);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenConfiguredSnapshotBoundIsNotPositive_ThrowsWithoutRegistering()
    {
        var services = new ServiceCollection();
        var before = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddSqliteSessionStore(Target(), static options => options.MaximumIssuedReadSnapshots = 0));

        exception.ParamName.ShouldBe("maximumIssuedReadSnapshots");
        services.Count.ShouldBe(before);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenConfiguredLockTimeoutIsFractional_ThrowsWithoutRegistering()
    {
        var services = new ServiceCollection();
        var before = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddSqliteSessionStore(Target(), static options => options.LockTimeout = TimeSpan.FromMilliseconds(1_500)));

        exception.ParamName.ShouldBe("lockTimeout");
        services.Count.ShouldBe(before);
    }

    [Fact]
    public void AddSqliteSessionStore_WhenServicesIsNull_ThrowsExactParameter()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionStore(Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionStore(Target(), static _ => { })).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddSqliteSessionStore_WhenTargetIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionStore(null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionStore(null!, static _ => { })).ParamName.ShouldBe("target");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddSqliteSessionStore_WhenConfigureIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionStore(Target(), (Action<SqliteSessionStoreOptions>) null!))
            .ParamName.ShouldBe("configure");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenCalledTwice_DoesNotRegisterDuplicateDirectory()
    {
        var target = Target();
        var services = new ServiceCollection();

        _ = services.AddSqliteSessionDirectory(_audience, target);
        _ = services.AddSqliteSessionDirectory(_audience, target);

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionDirectory)).ShouldBe(1);
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenNoSettingsSupplied_PublishesNoAmbientSettings()
    {
        var services = new ServiceCollection();

        _ = services.AddSqliteSessionDirectory(_audience, Target());

        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreSettings));
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreTarget));
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenConfigureSupplied_RunsDelegateOnceWithDefaultsAndResolvesDirectory()
    {
        var services = WithSecurityBoundaries();
        var invocations = 0;
        SqliteSessionStoreOptions? seen = null;

        var returned = services.AddSqliteSessionDirectory(_audience, Target(), options =>
        {
            invocations++;
            seen = options;
            options.LockTimeout = TimeSpan.FromSeconds(3);
            options.MaximumEntryPayloadBytes = 4_096;
            options.MaximumIssuedReadSnapshots = 32;
        });

        returned.ShouldBeSameAs(services);
        invocations.ShouldBe(1);
        _ = seen.ShouldNotBeNull();
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreOptions));
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(SqliteSessionStoreSettings));
        using var provider = services.BuildServiceProvider();
        var directory = provider.GetRequiredService<ISessionDirectory>().ShouldBeOfType<SqliteSessionDirectory>();
        directory.SecurityAudience.ShouldBe(_audience);
        directory.Durable.ShouldBeTrue();
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenCalledTwiceWithDifferentSettings_KeepsFirstDirectory()
    {
        // The directory uses TryAdd semantics: the first registration wins and later bounds are discarded.
        var target = Target();
        var services = WithSecurityBoundaries();
        var first = new SqliteSessionStoreSettings(TimeSpan.FromSeconds(2), 256, 4);

        _ = services.AddSqliteSessionDirectory(_audience, target, first);
        var descriptor = services.Single(static descriptor => descriptor.ServiceType == typeof(ISessionDirectory));
        _ = services.AddSqliteSessionDirectory(_audience, target, static options => options.MaximumIssuedReadSnapshots = 99);

        services.Single(static descriptor => descriptor.ServiceType == typeof(ISessionDirectory)).ShouldBeSameAs(descriptor);
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenConfiguredBoundIsInvalid_ThrowsWithoutRegistering()
    {
        var services = new ServiceCollection();
        var before = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddSqliteSessionDirectory(_audience, Target(), static options => options.LockTimeout = TimeSpan.Zero));

        exception.ParamName.ShouldBe("lockTimeout");
        services.Count.ShouldBe(before);
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenServicesIsNull_ThrowsExactParameter()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionDirectory(_audience, Target())).ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionDirectory(_audience, Target(), static _ => { })).ParamName.ShouldBe("services");
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenSecurityAudienceIsBlank_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddSqliteSessionDirectory(default, Target())).ParamName.ShouldBe("securityAudience");
        Should.Throw<ArgumentException>(() => services.AddSqliteSessionDirectory(default, Target(), static _ => { })).ParamName.ShouldBe("securityAudience");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenTargetIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionDirectory(_audience, null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => services.AddSqliteSessionDirectory(_audience, null!, static _ => { })).ParamName.ShouldBe("target");
        services.ShouldBeEmpty();
    }

    [Fact]
    public void AddSqliteSessionDirectory_WhenConfigureIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() =>
                services.AddSqliteSessionDirectory(_audience, Target(), (Action<SqliteSessionStoreOptions>) null!))
            .ParamName.ShouldBe("configure");
        services.ShouldBeEmpty();
    }

    private SqliteSessionStoreTarget Target() => new(
        Path.Combine(_directory, $"{Guid.NewGuid():N}.db"),
        new SqliteSessionStoreInstanceId(Guid.NewGuid()),
        SqliteDatabaseOpenMode.CreateIfMissing,
        SqliteSchemaMode.ApplyKnownMigrations);

    /// <summary>Creates a collection carrying the security boundaries a resolved store or directory requires.</summary>
    private static ServiceCollection WithSecurityBoundaries()
    {
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 9, 8, 12, 0, 0, TimeSpan.Zero));
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(timeProvider);
        _ = services.AddSingleton<ISecurityGrantStore>(new InMemorySecurityGrantStore(timeProvider));
        _ = services.AddSingleton<ISecurityAuditDispatcher>(new AcceptingAuditDispatcher());
        return services;
    }

    private sealed class AcceptingAuditDispatcher: ISecurityAuditDispatcher
    {
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }
}
