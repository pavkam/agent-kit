// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Json.Tests;

using AgentKit.Session;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>Verifies explicit, additive JSON session-store composition and idempotent directory composition.</summary>
public sealed class ServiceExtensionsTests: IDisposable
{
    private static readonly ComponentId _audience = new("tests.session.directory.json");
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"agentkit-session-json-di-{Guid.NewGuid():N}");

    /// <summary>Creates the isolated temporary root backing every target built by this fixture.</summary>
    public ServiceExtensionsTests() => Directory.CreateDirectory(_directory);

    /// <inheritdoc/>
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

    /// <summary>Verifies repeating the exact store registration is idempotent.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenCalledTwice_RegistersOneJsonStore()
    {
        var target = StoreTarget();
        var services = WithSecurityBoundaries();

        _ = services.AddJsonSessionStore(target);
        _ = services.AddJsonSessionStore(target);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        provider.GetServices<ISessionStore>().OfType<JsonSessionStore>().Count().ShouldBe(1);
    }

    /// <summary>Verifies store registrations are additive: a foreign store registered earlier stays visible.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenAnotherStoreAlreadyRegistered_RegistersBothStores()
    {
        var target = StoreTarget();
        var services = WithSecurityBoundaries();

        _ = services.AddSingleton<ISessionStore>(new FakeSessionStore());
        _ = services.AddJsonSessionStore(target);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        var stores = provider.GetServices<ISessionStore>().ToArray();
        stores.Select(static store => store.GetType()).ShouldBe([typeof(FakeSessionStore), typeof(JsonSessionStore)]);
    }

    /// <summary>Verifies registering the foreign store after the JSON store still leaves both visible.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenRegisteredBeforeAnotherStore_RegistersBothStores()
    {
        var services = new ServiceCollection();

        _ = services.AddJsonSessionStore(StoreTarget());
        _ = services.AddSingleton<ISessionStore>(new FakeSessionStore());

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionStore)).ShouldBe(2);
    }

    /// <summary>Verifies the target and settings are captured without publishing an ambient options singleton.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenSettingsSupplied_CapturesThemWithoutPublishingOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddJsonSessionStore(StoreTarget(), JsonSessionStoreSettings.CreateDefault());

        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionStoreOptions));
    }

    /// <summary>Verifies the configure delegate runs exactly once, receives fresh defaults, and its options are never registered.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenConfigureSupplied_InvokesDelegateOnceWithDefaultsAndRegistersStore()
    {
        var services = WithSecurityBoundaries();
        var invocations = 0;
        JsonSessionStoreOptions? observed = null;

        var returned = services.AddJsonSessionStore(StoreTarget(), options =>
        {
            invocations++;
            observed = options;
            options.MaximumIssuedReadSnapshots = 16;
        });

        returned.ShouldBeSameAs(services);
        invocations.ShouldBe(1);
        var seen = observed.ShouldNotBeNull();
        seen.MaximumRecordBytes.ShouldBe(JsonSessionStoreSettings.CreateDefault().MaximumRecordBytes);
        seen.MaximumIssuedReadSnapshots.ShouldBe(16);
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionStoreOptions));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionStoreSettings));
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        _ = provider.GetServices<ISessionStore>().ShouldHaveSingleItem().ShouldBeOfType<JsonSessionStore>();
    }

    /// <summary>Verifies repeating the configure overload with identical evidence still registers exactly one store.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenConfigureOverloadCalledTwiceWithTheSameEvidence_RegistersOneJsonStore()
    {
        var target = StoreTarget();
        var services = WithSecurityBoundaries();

        _ = services.AddJsonSessionStore(target, static options => options.MaximumIssuedReadSnapshots = 2);
        _ = services.AddJsonSessionStore(target, static options => options.MaximumIssuedReadSnapshots = 2);

        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
        provider.GetServices<ISessionStore>().OfType<JsonSessionStore>().Count().ShouldBe(1);
    }

    /// <summary>Verifies repeating registration with different effective settings for the same target fails closed.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenConfigureOverloadCalledTwiceWithDifferentEvidence_ThrowsInvalidOperationException()
    {
        var target = StoreTarget();
        var services = WithSecurityBoundaries();
        _ = services.AddJsonSessionStore(target, static options => options.MaximumIssuedReadSnapshots = 2);

        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddJsonSessionStore(target, static options => options.MaximumIssuedReadSnapshots = 3));
    }

    /// <summary>Verifies the resolved JSON store is a singleton across multiple resolutions.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenResolvedTwice_ReturnsTheSameSingletonInstance()
    {
        var services = WithSecurityBoundaries();
        _ = services.AddJsonSessionStore(StoreTarget());
        using var provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        var first = provider.GetRequiredService<ISessionStore>();
        var second = provider.GetRequiredService<ISessionStore>();

        first.ShouldBeSameAs(second);
    }

    /// <summary>Verifies a different target for the same leaf is rejected rather than silently repointed.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenCalledWithADifferentTarget_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddJsonSessionStore(StoreTarget());

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonSessionStore(StoreTarget()));
    }

    /// <summary>Verifies different settings for the same leaf and target is rejected.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenCalledWithDifferentSettingsForTheSameTarget_ThrowsInvalidOperationException()
    {
        var target = StoreTarget();
        var services = new ServiceCollection();
        _ = services.AddJsonSessionStore(target, JsonSessionStoreSettings.CreateDefault());

        var differentSettings = new JsonSessionStoreSettings(
            2_097_152, 2_097_152, 8_192, 8_192, JsonEncodingSettings.CreateDefault());

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonSessionStore(target, differentSettings));
    }

    /// <summary>Verifies a pre-registered <see cref="TimeProvider"/> is not displaced by the registration default.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenTimeProviderAlreadyRegistered_KeepsThePreRegisteredInstance()
    {
        var services = WithSecurityBoundaries();
        var timeProvider = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
        _ = services.AddSingleton<TimeProvider>(timeProvider);

        _ = services.AddJsonSessionStore(StoreTarget());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(timeProvider);
    }

    /// <summary>Verifies the additive session-entry codecs and their catalog are registered with <c>TryAdd</c> semantics.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenCodecAlreadyRegistered_DoesNotDisplaceIt()
    {
        var services = WithSecurityBoundaries();
        var customCodec = new MessageSessionEntryCodec();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<ISessionEntryCodec>(customCodec));

        _ = services.AddJsonSessionStore(StoreTarget());

        using var provider = services.BuildServiceProvider();
        provider.GetServices<ISessionEntryCodec>().ShouldContain(customCodec);
        _ = provider.GetRequiredService<ISessionEntryCodecCatalog>();
    }

    /// <summary>Verifies a null services collection is rejected for both store overloads.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenServicesIsNull_ThrowsExactParameter()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(StoreTarget()))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(StoreTarget(), static _ => { }))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(StoreTarget(), JsonSessionStoreSettings.CreateDefault()))
            .ParamName.ShouldBe("services");
    }

    /// <summary>Verifies a null target is rejected for every store overload.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenTargetIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(null!)).ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(null!, static _ => { }))
            .ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(null!, JsonSessionStoreSettings.CreateDefault()))
            .ParamName.ShouldBe("target");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies a null settings value is rejected for the explicit-settings overload.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenSettingsIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionStore(StoreTarget(), (JsonSessionStoreSettings) null!))
            .ParamName.ShouldBe("settings");
    }

    /// <summary>Verifies a foreign duplicate registration of the captured target type is rejected rather than silently picking one.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenTargetTypeIsAlreadyRegisteredTwice_ThrowsInvalidOperationException()
    {
        var target = StoreTarget();
        var services = new ServiceCollection();
        _ = services.AddSingleton(target);
        _ = services.AddSingleton(target);

        var exception = Should.Throw<InvalidOperationException>(() => services.AddJsonSessionStore(target));

        exception.Message.ShouldContain("not one exact captured instance");
    }

    /// <summary>Verifies a non-positive configured bound throws before any registration is added.</summary>
    [Fact]
    public void AddJsonSessionStore_WhenConfiguredBoundIsNotPositive_ThrowsWithoutRegistering()
    {
        var services = new ServiceCollection();
        var before = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddJsonSessionStore(StoreTarget(), options => options.MaximumRecordBytes = 0));

        exception.ParamName.ShouldBe("maximumRecordBytes");
        services.Count.ShouldBe(before);
    }

    /// <summary>Verifies repeating the directory registration does not create a duplicate.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenCalledTwice_DoesNotRegisterDuplicateDirectory()
    {
        var target = DirectoryTarget();
        var services = new ServiceCollection();

        _ = services.AddJsonSessionDirectory(_audience, target);
        _ = services.AddJsonSessionDirectory(_audience, target);

        services.Count(static descriptor => descriptor.ServiceType == typeof(ISessionDirectory)).ShouldBe(1);
    }

    /// <summary>Verifies no ambient mutable options singleton leaks from directory registration.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenNoSettingsSupplied_PublishesNoAmbientOptions()
    {
        var services = new ServiceCollection();

        _ = services.AddJsonSessionDirectory(_audience, DirectoryTarget());

        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionDirectoryOptions));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionDirectorySettings));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionDirectoryTarget));
    }

    /// <summary>Verifies the configure delegate runs exactly once and the resolved directory is the JSON type.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenConfigureSupplied_RunsDelegateOnceWithDefaultsAndResolvesDirectory()
    {
        var services = WithSecurityBoundaries();
        var invocations = 0;
        JsonSessionDirectoryOptions? seen = null;

        var returned = services.AddJsonSessionDirectory(_audience, DirectoryTarget(), options =>
        {
            invocations++;
            seen = options;
            options.MaximumRecordBytes = 4_096;
        });

        returned.ShouldBeSameAs(services);
        invocations.ShouldBe(1);
        _ = seen.ShouldNotBeNull();
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionDirectoryOptions));
        services.ShouldContain(static descriptor => descriptor.ServiceType == typeof(JsonSessionDirectorySettings));
        using var provider = services.BuildServiceProvider();
        var directory = provider.GetRequiredService<ISessionDirectory>().ShouldBeOfType<JsonSessionDirectory>();
        directory.SecurityAudience.ShouldBe(_audience);
        directory.Durable.ShouldBeTrue();
    }

    /// <summary>Verifies repeating directory registration with identical effective settings for the same target is idempotent.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenCalledTwiceWithTheSameEffectiveSettings_KeepsOneDirectory()
    {
        var target = DirectoryTarget();
        var services = WithSecurityBoundaries();
        _ = services.AddJsonSessionDirectory(_audience, target, JsonSessionDirectorySettings.CreateDefault());
        var descriptor = services.Single(static descriptor => descriptor.ServiceType == typeof(ISessionDirectory));

        _ = services.AddJsonSessionDirectory(_audience, target, JsonSessionDirectorySettings.CreateDefault());

        services.Single(static descriptor => descriptor.ServiceType == typeof(ISessionDirectory)).ShouldBeSameAs(descriptor);
    }

    /// <summary>Verifies repeating directory registration with different effective settings for the same target fails closed.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenCalledTwiceWithDifferentSettingsForSameTarget_ThrowsInvalidOperationException()
    {
        var target = DirectoryTarget();
        var services = WithSecurityBoundaries();
        _ = services.AddJsonSessionDirectory(_audience, target, JsonSessionDirectorySettings.CreateDefault());

        _ = Should.Throw<InvalidOperationException>(() =>
            services.AddJsonSessionDirectory(_audience, target, static options => options.MaximumRecordBytes = 99));
    }

    /// <summary>Verifies a pre-registered directory from another package stays resolved instead of being replaced.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenAnotherDirectoryAlreadyRegistered_KeepsTheForeignDirectory()
    {
        var services = new ServiceCollection();
        var foreign = new FakeSessionDirectory();
        _ = services.AddSingleton<ISessionDirectory>(foreign);

        _ = services.AddJsonSessionDirectory(_audience, DirectoryTarget());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<ISessionDirectory>().ShouldBeSameAs(foreign);
    }

    /// <summary>Verifies a different target for the same directory leaf is rejected.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenCalledWithADifferentTarget_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddJsonSessionDirectory(_audience, DirectoryTarget());

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonSessionDirectory(_audience, DirectoryTarget()));
    }

    /// <summary>Verifies a non-positive configured bound is rejected before any registration is added.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenConfiguredBoundIsInvalid_ThrowsWithoutRegistering()
    {
        var services = new ServiceCollection();
        var before = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(() =>
            services.AddJsonSessionDirectory(_audience, DirectoryTarget(), static options => options.MaximumRecordBytes = 0));

        exception.ParamName.ShouldBe("maximumRecordBytes");
        services.Count.ShouldBe(before);
    }

    /// <summary>Verifies a null services collection is rejected for every directory overload.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenServicesIsNull_ThrowsExactParameter()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionDirectory(_audience, DirectoryTarget()))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionDirectory(_audience, DirectoryTarget(), static _ => { }))
            .ParamName.ShouldBe("services");
        Should.Throw<ArgumentNullException>(() =>
                services.AddJsonSessionDirectory(_audience, DirectoryTarget(), JsonSessionDirectorySettings.CreateDefault()))
            .ParamName.ShouldBe("services");
    }

    /// <summary>Verifies a blank security audience is rejected for every directory overload.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenSecurityAudienceIsBlank_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentException>(() => services.AddJsonSessionDirectory(default, DirectoryTarget()))
            .ParamName.ShouldBe("securityAudience");
        Should.Throw<ArgumentException>(() => services.AddJsonSessionDirectory(default, DirectoryTarget(), static _ => { }))
            .ParamName.ShouldBe("securityAudience");
        Should.Throw<ArgumentException>(() =>
                services.AddJsonSessionDirectory(default, DirectoryTarget(), JsonSessionDirectorySettings.CreateDefault()))
            .ParamName.ShouldBe("securityAudience");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies a null target is rejected for every directory overload.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenTargetIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionDirectory(_audience, null!))
            .ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() => services.AddJsonSessionDirectory(_audience, null!, static _ => { }))
            .ParamName.ShouldBe("target");
        Should.Throw<ArgumentNullException>(() =>
                services.AddJsonSessionDirectory(_audience, null!, JsonSessionDirectorySettings.CreateDefault()))
            .ParamName.ShouldBe("target");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies a null settings value is rejected for the explicit-settings directory overload.</summary>
    [Fact]
    public void AddJsonSessionDirectory_WhenSettingsIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() =>
                new ServiceCollection().AddJsonSessionDirectory(
                    _audience, DirectoryTarget(), (JsonSessionDirectorySettings) null!))
            .ParamName.ShouldBe("settings");

    private JsonSessionStoreTarget StoreTarget() => new(
        Path.Combine(_directory, $"{Guid.NewGuid():N}"),
        new JsonSessionStoreInstanceId(Guid.NewGuid()),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends);

    private JsonSessionDirectoryTarget DirectoryTarget() => new(
        Path.Combine(_directory, $"{Guid.NewGuid():N}"),
        new JsonSessionDirectoryInstanceId(Guid.NewGuid()),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends);

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
        public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
            SecurityAuditRecord record, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<SecurityAuditDispatchResult>(new SecurityAuditAccepted());
    }
}
