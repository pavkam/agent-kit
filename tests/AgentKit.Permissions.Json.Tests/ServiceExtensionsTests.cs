// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.Json.Tests;

/// <summary>Verifies explicit, order-independent registration of the durable JSON grant-store and approval-store leaves.</summary>
public sealed class ServiceExtensionsTests
{
    // -- Grant store: settings overload --------------------------------------------------------------------------

    /// <summary>Verifies every missing registration input to the settings overload is rejected with its exact parameter before mutation.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenSettingsOverloadArgumentIsNull_ThrowsExactArgument()
    {
        var target = CreateGrantTarget();
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();

        var nullServices = Should.Throw<ArgumentNullException>(
            () => ServiceExtensions.AddJsonSecurityGrantStore(null!, target, settings));
        var nullTarget = Should.Throw<ArgumentNullException>(
            () => services.AddJsonSecurityGrantStore(null!, settings));
        var nullSettings = Should.Throw<ArgumentNullException>(
            () => services.AddJsonSecurityGrantStore(target, (JsonSecurityGrantStoreSettings) null!));

        nullServices.ParamName.ShouldBe("services");
        nullTarget.ParamName.ShouldBe("target");
        nullSettings.ParamName.ShouldBe("settings");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies exact repeats share one selected uninitialized singleton.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenRepeatedExactly_RegistersOneSingleton()
    {
        var target = CreateGrantTarget();
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();

        _ = services.AddJsonSecurityGrantStore(target, settings);
        var countAfterFirst = services.Count;
        _ = services.AddJsonSecurityGrantStore(target, settings);

        services.Count.ShouldBe(countAfterFirst);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var stores = provider.GetServices<ISecurityGrantStore>().ToArray();
        _ = stores.ShouldHaveSingleItem().ShouldBeOfType<JsonSecurityGrantStore>();
        provider.GetRequiredService<ISecurityGrantStore>().ShouldBeSameAs(stores[0]);
        Directory.Exists(target.DirectoryPath).ShouldBeTrue();
        File.Exists(Path.Combine(target.DirectoryPath, "store.json")).ShouldBeFalse();
    }

    /// <summary>Verifies a repeat with a different target is rejected before collection mutation.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenRepeatTargetDiffers_RejectsBeforeMutation()
    {
        var first = CreateGrantTarget();
        var second = CreateGrantTarget();
        var settings = JsonSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        _ = services.AddJsonSecurityGrantStore(first, settings);
        var count = services.Count;

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonSecurityGrantStore(second, settings));

        services.Count.ShouldBe(count);
    }

    /// <summary>Verifies a repeat with different settings is rejected before collection mutation.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenRepeatSettingsDiffer_RejectsBeforeMutation()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();
        _ = services.AddJsonSecurityGrantStore(target, JsonSecurityGrantStoreSettings.CreateDefault());
        var count = services.Count;
        var differentSettings = new JsonSecurityGrantStoreSettings(2048, 2048, 8, JsonEncodingSettings.CreateDefault());

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddJsonSecurityGrantStore(target, differentSettings));

        services.Count.ShouldBe(count);
    }

    /// <summary>Verifies a foreign custom registration remains visible so composition can reject ambiguity.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenCustomStoreExists_PreservesVisibleAmbiguity()
    {
        var custom = new FixedGrantStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISecurityGrantStore>(custom);

        _ = services.AddJsonSecurityGrantStore(CreateGrantTarget(), JsonSecurityGrantStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var stores = provider.GetServices<ISecurityGrantStore>().ToArray();
        stores.Length.ShouldBe(2);
        stores.ShouldContain(custom);
        stores.ShouldContain(static store => store is JsonSecurityGrantStore);
    }

    /// <summary>Verifies more than one ambiguous unkeyed captured target already registered is rejected rather than arbitrarily chosen.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenMultipleUnkeyedTargetsAreAlreadyRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(CreateGrantTarget());
        _ = services.AddSingleton(CreateGrantTarget());

        var exception = Should.Throw<InvalidOperationException>(() => services.AddJsonSecurityGrantStore(
            CreateGrantTarget(), JsonSecurityGrantStoreSettings.CreateDefault()));

        exception.Message.ShouldContain(nameof(JsonSecurityGrantStoreTarget));
    }

    /// <summary>Verifies a single unkeyed captured registration produced by a factory rather than an instance is rejected as ambiguous.</summary>
    /// <remarks>A factory-based descriptor has no eagerly captured instance, so it can never satisfy exact-instance capture semantics.</remarks>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenUnkeyedTargetIsFactoryRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(static _ => CreateGrantTarget());

        var exception = Should.Throw<InvalidOperationException>(() => services.AddJsonSecurityGrantStore(
            CreateGrantTarget(), JsonSecurityGrantStoreSettings.CreateDefault()));

        exception.Message.ShouldContain(nameof(JsonSecurityGrantStoreTarget));
    }

    /// <summary>Verifies unrelated keyed leaf configuration under a different key does not alter the captured unkeyed evidence.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenOtherKeyHasConfiguration_IgnoresKeyedEvidence()
    {
        var expectedTarget = CreateGrantTarget();
        var expectedSettings = JsonSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton("other", CreateGrantTarget());
        _ = services.AddKeyedSingleton(
            "other", new JsonSecurityGrantStoreSettings(2048, 2048, 8, JsonEncodingSettings.CreateDefault()));

        _ = services.AddJsonSecurityGrantStore(expectedTarget, expectedSettings);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<JsonSecurityGrantStoreTarget>().ShouldBe(expectedTarget);
        provider.GetRequiredService<JsonSecurityGrantStoreSettings>().ShouldBe(expectedSettings);
        provider.GetRequiredKeyedService<JsonSecurityGrantStoreTarget>("other").ShouldNotBe(expectedTarget);
        _ = provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<JsonSecurityGrantStore>();
    }

    /// <summary>Verifies null-keyed captured values participate in the same unkeyed selection semantics as ordinary registrations.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenNullKeyHasConfiguration_ReusesCapturedEvidence()
    {
        var expectedTarget = CreateGrantTarget();
        var expectedSettings = JsonSecurityGrantStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        _ = services.AddKeyedSingleton<JsonSecurityGrantStoreTarget>(null, expectedTarget);
        _ = services.AddKeyedSingleton<JsonSecurityGrantStoreSettings>(null, expectedSettings);

        _ = services.AddJsonSecurityGrantStore(expectedTarget, expectedSettings);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<JsonSecurityGrantStoreTarget>().ShouldBe(expectedTarget);
        provider.GetRequiredService<JsonSecurityGrantStoreSettings>().ShouldBe(expectedSettings);
        services.Count(static descriptor => descriptor.ServiceType == typeof(JsonSecurityGrantStoreTarget))
            .ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(JsonSecurityGrantStoreSettings))
            .ShouldBe(1);
    }

    // -- Grant store: configure overload --------------------------------------------------------------------------

    /// <summary>Verifies the configure overload rejects missing inputs with the exact parameter and never runs the delegate.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenConfigureOverloadArgumentIsNull_ThrowsExactArgumentWithoutInvokingDelegate()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();
        var invoked = false;
        void Configure(JsonSecurityGrantStoreOptions options) => invoked = true;

        var nullServices = Should.Throw<ArgumentNullException>(
            () => ServiceExtensions.AddJsonSecurityGrantStore(null!, target, Configure));
        var nullTarget = Should.Throw<ArgumentNullException>(
            () => services.AddJsonSecurityGrantStore(null!, Configure));

        nullServices.ParamName.ShouldBe("services");
        nullTarget.ParamName.ShouldBe("target");
        invoked.ShouldBeFalse();
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies the configure delegate runs exactly once and its options instance is never registered in dependency injection.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenConfigureIsProvided_RunsDelegateExactlyOnceWithoutRegisteringOptions()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();
        var invocationCount = 0;

        _ = services.AddJsonSecurityGrantStore(target, options =>
        {
            invocationCount++;
            options.CompactionRecordThreshold = 7;
        });

        invocationCount.ShouldBe(1);
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(JsonSecurityGrantStoreOptions));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<JsonSecurityGrantStoreSettings>().CompactionRecordThreshold.ShouldBe(7);
    }

    /// <summary>Verifies omitting the delegate captures the documented default settings.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenConfigureIsOmitted_CapturesDefaultSettings()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();

        _ = services.AddJsonSecurityGrantStore(target);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<JsonSecurityGrantStoreSettings>()
            .ShouldBe(JsonSecurityGrantStoreSettings.CreateDefault());
        _ = provider.GetRequiredService<ISecurityGrantStore>().ShouldBeOfType<JsonSecurityGrantStore>();
    }

    /// <summary>Verifies invalid configured bounds fail eagerly at registration before any descriptor is added.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenConfiguredValueIsInvalid_ThrowsBeforeMutation()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddJsonSecurityGrantStore(target, static options => options.MaximumRecordBytes = 0));

        exception.ParamName.ShouldBe("maximumRecordBytes");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies repeating the same effective configure delegate is idempotent.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenSameConfigureRepeats_IsIdempotent()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();
        static void Configure(JsonSecurityGrantStoreOptions options) => options.CompactionRecordThreshold = 11;

        _ = services.AddJsonSecurityGrantStore(target, Configure);
        var count = services.Count;
        _ = services.AddJsonSecurityGrantStore(target, Configure);

        services.Count.ShouldBe(count);
        services.Count(static descriptor => descriptor.ServiceType == typeof(JsonSecurityGrantStoreSettings))
            .ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(ISecurityGrantStore)).ShouldBe(1);
    }

    /// <summary>Verifies a repeat with different effective configured settings is rejected before collection mutation.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenDifferentConfigureRepeats_RejectsBeforeMutation()
    {
        var target = CreateGrantTarget();
        var services = new ServiceCollection();
        _ = services.AddJsonSecurityGrantStore(target, static options => options.CompactionRecordThreshold = 7);
        var count = services.Count;

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddJsonSecurityGrantStore(target, static options => options.CompactionRecordThreshold = 8));

        services.Count.ShouldBe(count);
    }

    /// <summary>Verifies equal effective settings are idempotent across the settings and configure overloads in either order.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenOverloadsProduceEqualSettings_IsIdempotentAcrossOverloads()
    {
        var target = CreateGrantTarget();
        var settings = new JsonSecurityGrantStoreSettings(100, 200, 10, JsonEncodingSettings.CreateDefault());
        static void Configure(JsonSecurityGrantStoreOptions options)
        {
            options.MaximumRecordBytes = 100;
            options.MaximumDocumentBytes = 200;
            options.CompactionRecordThreshold = 10;
        }

        var settingsFirst = new ServiceCollection();
        _ = settingsFirst.AddJsonSecurityGrantStore(target, settings);
        var settingsFirstCount = settingsFirst.Count;
        _ = Should.NotThrow(() => settingsFirst.AddJsonSecurityGrantStore(target, Configure));

        var configureFirst = new ServiceCollection();
        _ = configureFirst.AddJsonSecurityGrantStore(target, Configure);
        var configureFirstCount = configureFirst.Count;
        _ = Should.NotThrow(() => configureFirst.AddJsonSecurityGrantStore(target, settings));

        settingsFirst.Count.ShouldBe(settingsFirstCount);
        configureFirst.Count.ShouldBe(configureFirstCount);
    }

    // -- Grant store: TimeProvider and resolution ------------------------------------------------------------------

    /// <summary>Verifies TimeProvider.System is added only when no TimeProvider is already registered.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenTimeProviderAlreadyRegistered_PreservesExistingTimeProvider()
    {
        var custom = new FakeTimeProvider();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(custom);

        _ = services.AddJsonSecurityGrantStore(CreateGrantTarget(), JsonSecurityGrantStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(custom);
    }

    /// <summary>Verifies TimeProvider.System becomes available when no TimeProvider was registered beforehand.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenNoTimeProviderRegistered_AddsSystemTimeProvider()
    {
        var services = new ServiceCollection();

        _ = services.AddJsonSecurityGrantStore(CreateGrantTarget(), JsonSecurityGrantStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(TimeProvider.System);
    }

    /// <summary>Verifies the resolved grant-store interface is the concrete JSON adapter and is registered as a singleton.</summary>
    [Fact]
    public void AddJsonSecurityGrantStore_WhenResolvedTwice_ReturnsSameSingletonInstance()
    {
        var services = new ServiceCollection();
        _ = services.AddJsonSecurityGrantStore(CreateGrantTarget(), JsonSecurityGrantStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var first = provider.GetRequiredService<ISecurityGrantStore>();
        var second = provider.GetRequiredService<ISecurityGrantStore>();

        _ = first.ShouldBeOfType<JsonSecurityGrantStore>();
        first.ShouldBeSameAs(second);
    }

    // -- Approval store: settings overload -----------------------------------------------------------------------

    /// <summary>Verifies every missing registration input to the settings overload is rejected with its exact parameter before mutation.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenSettingsOverloadArgumentIsNull_ThrowsExactArgument()
    {
        var target = CreateApprovalTarget();
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var services = new ServiceCollection();

        var nullServices = Should.Throw<ArgumentNullException>(
            () => ServiceExtensions.AddJsonApprovalStore(null!, target, settings));
        var nullTarget = Should.Throw<ArgumentNullException>(
            () => services.AddJsonApprovalStore(null!, settings));
        var nullSettings = Should.Throw<ArgumentNullException>(
            () => services.AddJsonApprovalStore(target, (JsonApprovalStoreSettings) null!));

        nullServices.ParamName.ShouldBe("services");
        nullTarget.ParamName.ShouldBe("target");
        nullSettings.ParamName.ShouldBe("settings");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies exact repeats share one selected uninitialized singleton.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenRepeatedExactly_RegistersOneSingleton()
    {
        var target = CreateApprovalTarget();
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var services = new ServiceCollection();

        _ = services.AddJsonApprovalStore(target, settings);
        var countAfterFirst = services.Count;
        _ = services.AddJsonApprovalStore(target, settings);

        services.Count.ShouldBe(countAfterFirst);
        using var provider = services.BuildServiceProvider(validateScopes: true);
        var stores = provider.GetServices<IApprovalStore>().ToArray();
        _ = stores.ShouldHaveSingleItem().ShouldBeOfType<JsonApprovalStore>();
        provider.GetRequiredService<IApprovalStore>().ShouldBeSameAs(stores[0]);
    }

    /// <summary>Verifies a repeat with a different target is rejected before collection mutation.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenRepeatTargetDiffers_RejectsBeforeMutation()
    {
        var first = CreateApprovalTarget();
        var second = CreateApprovalTarget();
        var settings = JsonApprovalStoreSettings.CreateDefault();
        var services = new ServiceCollection();
        _ = services.AddJsonApprovalStore(first, settings);
        var count = services.Count;

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonApprovalStore(second, settings));

        services.Count.ShouldBe(count);
    }

    /// <summary>Verifies a repeat with different settings is rejected before collection mutation.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenRepeatSettingsDiffer_RejectsBeforeMutation()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();
        _ = services.AddJsonApprovalStore(target, JsonApprovalStoreSettings.CreateDefault());
        var count = services.Count;
        var differentSettings = new JsonApprovalStoreSettings(2048, 2048, 8, JsonEncodingSettings.CreateDefault());

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonApprovalStore(target, differentSettings));

        services.Count.ShouldBe(count);
    }

    /// <summary>Verifies a foreign custom registration remains visible so composition can reject ambiguity.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenCustomStoreExists_PreservesVisibleAmbiguity()
    {
        var custom = new FixedApprovalStore();
        var services = new ServiceCollection();
        _ = services.AddSingleton<IApprovalStore>(custom);

        _ = services.AddJsonApprovalStore(CreateApprovalTarget(), JsonApprovalStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var stores = provider.GetServices<IApprovalStore>().ToArray();
        stores.Length.ShouldBe(2);
        stores.ShouldContain(custom);
        stores.ShouldContain(static store => store is JsonApprovalStore);
    }

    /// <summary>Verifies more than one ambiguous unkeyed captured target already registered is rejected rather than arbitrarily chosen.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenMultipleUnkeyedTargetsAreAlreadyRegistered_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(CreateApprovalTarget());
        _ = services.AddSingleton(CreateApprovalTarget());

        var exception = Should.Throw<InvalidOperationException>(() => services.AddJsonApprovalStore(
            CreateApprovalTarget(), JsonApprovalStoreSettings.CreateDefault()));

        exception.Message.ShouldContain(nameof(JsonApprovalStoreTarget));
    }

    // -- Approval store: configure overload -----------------------------------------------------------------------

    /// <summary>Verifies the configure overload rejects missing inputs with the exact parameter and never runs the delegate.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenConfigureOverloadArgumentIsNull_ThrowsExactArgumentWithoutInvokingDelegate()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();
        var invoked = false;
        void Configure(JsonApprovalStoreOptions options) => invoked = true;

        var nullServices = Should.Throw<ArgumentNullException>(
            () => ServiceExtensions.AddJsonApprovalStore(null!, target, Configure));
        var nullTarget = Should.Throw<ArgumentNullException>(
            () => services.AddJsonApprovalStore(null!, Configure));

        nullServices.ParamName.ShouldBe("services");
        nullTarget.ParamName.ShouldBe("target");
        invoked.ShouldBeFalse();
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies the configure delegate runs exactly once and its options instance is never registered in dependency injection.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenConfigureIsProvided_RunsDelegateExactlyOnceWithoutRegisteringOptions()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();
        var invocationCount = 0;

        _ = services.AddJsonApprovalStore(target, options =>
        {
            invocationCount++;
            options.CompactionRecordThreshold = 9;
        });

        invocationCount.ShouldBe(1);
        services.ShouldNotContain(static descriptor => descriptor.ServiceType == typeof(JsonApprovalStoreOptions));
        using var provider = services.BuildServiceProvider(validateScopes: true);
        provider.GetRequiredService<JsonApprovalStoreSettings>().CompactionRecordThreshold.ShouldBe(9);
    }

    /// <summary>Verifies omitting the delegate captures the documented default settings.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenConfigureIsOmitted_CapturesDefaultSettings()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();

        _ = services.AddJsonApprovalStore(target);
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<JsonApprovalStoreSettings>().ShouldBe(JsonApprovalStoreSettings.CreateDefault());
        _ = provider.GetRequiredService<IApprovalStore>().ShouldBeOfType<JsonApprovalStore>();
    }

    /// <summary>Verifies invalid configured bounds fail eagerly at registration before any descriptor is added.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenConfiguredValueIsInvalid_ThrowsBeforeMutation()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddJsonApprovalStore(target, static options => options.MaximumDocumentBytes = -1));

        exception.ParamName.ShouldBe("maximumDocumentBytes");
        services.ShouldBeEmpty();
    }

    /// <summary>Verifies repeating the same effective configure delegate is idempotent.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenSameConfigureRepeats_IsIdempotent()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();
        static void Configure(JsonApprovalStoreOptions options) => options.CompactionRecordThreshold = 13;

        _ = services.AddJsonApprovalStore(target, Configure);
        var count = services.Count;
        _ = services.AddJsonApprovalStore(target, Configure);

        services.Count.ShouldBe(count);
        services.Count(static descriptor => descriptor.ServiceType == typeof(JsonApprovalStoreSettings)).ShouldBe(1);
        services.Count(static descriptor => descriptor.ServiceType == typeof(IApprovalStore)).ShouldBe(1);
    }

    /// <summary>Verifies a repeat with different effective configured settings is rejected before collection mutation.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenDifferentConfigureRepeats_RejectsBeforeMutation()
    {
        var target = CreateApprovalTarget();
        var services = new ServiceCollection();
        _ = services.AddJsonApprovalStore(target, static options => options.CompactionRecordThreshold = 7);
        var count = services.Count;

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddJsonApprovalStore(target, static options => options.CompactionRecordThreshold = 8));

        services.Count.ShouldBe(count);
    }

    // -- Approval store: TimeProvider and resolution ---------------------------------------------------------------

    /// <summary>Verifies TimeProvider.System is added only when no TimeProvider is already registered.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenTimeProviderAlreadyRegistered_PreservesExistingTimeProvider()
    {
        var custom = new FakeTimeProvider();
        var services = new ServiceCollection();
        _ = services.AddSingleton<TimeProvider>(custom);

        _ = services.AddJsonApprovalStore(CreateApprovalTarget(), JsonApprovalStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(custom);
    }

    /// <summary>Verifies the resolved approval-store interface is the concrete JSON adapter and is registered as a singleton.</summary>
    [Fact]
    public void AddJsonApprovalStore_WhenResolvedTwice_ReturnsSameSingletonInstance()
    {
        var services = new ServiceCollection();
        _ = services.AddJsonApprovalStore(CreateApprovalTarget(), JsonApprovalStoreSettings.CreateDefault());
        using var provider = services.BuildServiceProvider(validateScopes: true);

        var first = provider.GetRequiredService<IApprovalStore>();
        var second = provider.GetRequiredService<IApprovalStore>();

        _ = first.ShouldBeOfType<JsonApprovalStore>();
        first.ShouldBeSameAs(second);
    }

    // -- Shared helpers ----------------------------------------------------------------------------------------

    private static JsonSecurityGrantStoreTarget CreateGrantTarget() => new(
        TestTemporaryDirectory.Create(),
        new JsonSecurityGrantStoreInstanceId(Guid.NewGuid()),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends);

    private static JsonApprovalStoreTarget CreateApprovalTarget() => new(
        TestTemporaryDirectory.Create(),
        new JsonApprovalStoreInstanceId(Guid.NewGuid()),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends);

    private sealed class FixedGrantStore: ISecurityGrantStore
    {
        public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default) =>
            ValueTask.CompletedTask;

        public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
            SecurityGrant grant, SecurityEnforcementRequest enforcement, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new GrantConsumptionResult(GrantConsumptionStatus.Unknown, 0, "No effect."));

        public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(false);
    }

    private sealed class FixedApprovalStore: IApprovalStore
    {
        public ApprovalStoreCapabilities Capabilities { get; } = new(IsDurable: false, ProvidesTrustedControlPlane: false);

        public ValueTask<ApprovalStoreCreateResult> CreateAsync(
            ApprovalRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ApprovalStoreCreateResult.Created);

        public ValueTask<ApprovalStoreResolveResult> ResolveAsync(
            ApprovalResponse response, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ApprovalStoreResolveResult.Resolved);

        public ValueTask<ApprovalStoreReadResult> ReadAsync(
            ApprovalRequestId requestId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(new ApprovalStoreReadResult(null, null));
    }
}
