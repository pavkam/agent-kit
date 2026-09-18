// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

/// <summary>Verifies explicit additive JSON budget-ledger composition through both registration overloads.</summary>
/// <remarks>
/// These cases prove the registration extension validates its own arguments and captured evidence before touching
/// dependency injection, keeps the configure delegate's mutable options out of the container, and stays idempotent for
/// repeated exact registrations while rejecting a later composition that would silently redefine the store's evidence
/// bounds or target.
/// </remarks>
public sealed class ServiceExtensionsTests
{
    /// <summary>Verifies the settings overload attributes a null caller collection before inspecting configuration.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenServicesIsNullAndSettingsOverload_ThrowsExactParameter()
    {
        IServiceCollection services = null!;

        Should.Throw<ArgumentNullException>(
                () => services.AddJsonBudgetLedger(null!, (JsonBudgetLedgerSettings) null!))
            .ParamName.ShouldBe("services");
    }

    /// <summary>Verifies the configure overload attributes a null caller collection before running the delegate.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenServicesIsNullAndConfigureOverload_ThrowsExactParameter()
    {
        IServiceCollection services = null!;
        var invoked = false;

        Should.Throw<ArgumentNullException>(() => services.AddJsonBudgetLedger(CreateTarget(), _ => invoked = true))
            .ParamName.ShouldBe("services");
        invoked.ShouldBeFalse();
    }

    /// <summary>Verifies the configure overload attributes a null target before running the delegate or registering anything.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenTargetIsNullAndConfigureOverload_ThrowsExactParameter()
    {
        var services = new ServiceCollection();
        var invoked = false;

        Should.Throw<ArgumentNullException>(() => services.AddJsonBudgetLedger(null!, _ => invoked = true))
            .ParamName.ShouldBe("target");
        invoked.ShouldBeFalse();
        services.Count.ShouldBe(0);
    }

    /// <summary>Verifies the settings overload attributes a null target before touching the caller collection further.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenTargetIsNullAndSettingsOverload_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(
                () => services.AddJsonBudgetLedger(null!, JsonBudgetLedgerSettings.CreateDefault()))
            .ParamName.ShouldBe("target");
        services.Count.ShouldBe(0);
    }

    /// <summary>Verifies the settings overload attributes a null settings instance.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenSettingsIsNull_ThrowsExactParameter()
    {
        var services = new ServiceCollection();

        Should.Throw<ArgumentNullException>(() => services.AddJsonBudgetLedger(CreateTarget(), (JsonBudgetLedgerSettings) null!))
            .ParamName.ShouldBe("settings");
    }

    /// <summary>Verifies calling with only a target binds to the configure overload and captures default settings.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenConfigureIsOmitted_CapturesDefaultSettings()
    {
        var services = new ServiceCollection();

        _ = services.AddJsonBudgetLedger(CreateTarget());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<JsonBudgetLedgerSettings>().ShouldBe(JsonBudgetLedgerSettings.CreateDefault());
        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(1);
    }

    /// <summary>Verifies a configure delegate's values reach the captured immutable settings singleton, and the mutable
    /// options instance itself is never registered in the container.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenConfigureIsSupplied_CapturesConfiguredSettingsAndHidesOptions()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        var invocations = 0;

        _ = services.AddJsonBudgetLedger(target, options =>
        {
            invocations++;
            options.MaximumRecordBytes = 4_096;
            options.MaximumDocumentBytes = 8_192;
            options.Encoding.SerializerOptions.WriteIndented = true;
        });

        invocations.ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        var settings = provider.GetRequiredService<JsonBudgetLedgerSettings>();
        settings.MaximumRecordBytes.ShouldBe(4_096);
        settings.MaximumDocumentBytes.ShouldBe(8_192);
        provider.GetRequiredService<JsonBudgetLedgerTarget>().ShouldBeSameAs(target);
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(JsonBudgetLedgerOptions));
    }

    /// <summary>Verifies an invalid configured byte bound is rejected at registration before any descriptor is added.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenConfiguredValueIsInvalid_ThrowsBeforeRegistering()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IBudgetLedger, StubLedger>();
        var countBefore = services.Count;

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddJsonBudgetLedger(CreateTarget(), static options => options.MaximumRecordBytes = 0));

        exception.ParamName.ShouldBe("maximumRecordBytes");
        services.Count.ShouldBe(countBefore);
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(JsonBudgetLedgerTarget));
        services.ShouldNotContain(descriptor => descriptor.ServiceType == typeof(JsonBudgetLedgerSettings));
    }

    /// <summary>Verifies a non-positive configured document bound is rejected with the settings constructor's parameter name.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenConfiguredDocumentBoundIsInvalid_ThrowsBeforeRegistering()
    {
        var services = new ServiceCollection();

        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => services.AddJsonBudgetLedger(CreateTarget(), static options => options.MaximumDocumentBytes = -1));

        exception.ParamName.ShouldBe("maximumDocumentBytes");
        services.Count.ShouldBe(0);
    }

    /// <summary>Verifies repeating the configure overload with the same effective settings is idempotent.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenSameConfigureRepeated_IsIdempotent()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        static void Configure(JsonBudgetLedgerOptions options) => options.MaximumRecordBytes = 4_096;

        _ = services.AddJsonBudgetLedger(target, Configure);
        var countAfterFirst = services.Count;
        _ = services.AddJsonBudgetLedger(target, Configure);

        services.Count.ShouldBe(countAfterFirst);
        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(JsonBudgetLedgerSettings)).ShouldBe(1);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<JsonBudgetLedgerSettings>().MaximumRecordBytes.ShouldBe(4_096);
    }

    /// <summary>Verifies repeating the configure overload with different effective settings is rejected.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenDifferentConfigureRepeated_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        _ = services.AddJsonBudgetLedger(target, static options => options.MaximumRecordBytes = 4_096);

        _ = Should.Throw<InvalidOperationException>(
            () => services.AddJsonBudgetLedger(target, static options => options.MaximumRecordBytes = 8_192));
    }

    /// <summary>Verifies the configure overload and the explicit settings overload agree on equality for conflict detection.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenConfigureMatchesExplicitSettings_IsIdempotentAcrossOverloads()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        _ = services.AddJsonBudgetLedger(target, JsonBudgetLedgerSettings.CreateDefault());
        var countAfterFirst = services.Count;

        _ = services.AddJsonBudgetLedger(target);

        services.Count.ShouldBe(countAfterFirst);
    }

    /// <summary>Verifies a pre-registered clock is preserved instead of being displaced by the leaf's default.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenTimeProviderAlreadyRegistered_PreservesExistingRegistration()
    {
        var services = new ServiceCollection();
        var clock = new FakeTimeProvider();
        _ = services.AddSingleton<TimeProvider>(clock);

        _ = services.AddJsonBudgetLedger(CreateTarget());

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<TimeProvider>().ShouldBeSameAs(clock);
    }

    /// <summary>Verifies the resolved ledger is the concrete JSON adapter registered as a singleton.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenResolved_IsSingletonJsonBudgetLedger()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton<IBudgetDimensionCatalog, EmptyDimensionCatalog>();

        _ = services.AddJsonBudgetLedger(CreateTarget());

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IBudgetLedger>();
        var second = provider.GetRequiredService<IBudgetLedger>();
        _ = first.ShouldBeOfType<JsonBudgetLedger>();
        first.ShouldBeSameAs(second);
    }

    /// <summary>Verifies exact repeats are idempotent while a competing ledger and pre-registered identity generators remain visible.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenRepeatedAndCompeting_PreservesVisibleSelections()
    {
        var services = new ServiceCollection();
        var scopes = new ScopeIds();
        var reservations = new ReservationIds();
        _ = services.AddSingleton<IIdentifierGenerator<BudgetScopeId>>(scopes);
        _ = services.AddSingleton<IIdentifierGenerator<BudgetReservationId>>(reservations);
        var target = CreateTarget();
        var settings = JsonBudgetLedgerSettings.CreateDefault();
        _ = services.AddJsonBudgetLedger(target, settings);
        _ = services.AddJsonBudgetLedger(target, settings);
        _ = services.AddSingleton<IBudgetLedger, StubLedger>();

        services.Count(descriptor => descriptor.ServiceType == typeof(IBudgetLedger)).ShouldBe(2);
        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IIdentifierGenerator<BudgetScopeId>>().ShouldBeSameAs(scopes);
        provider.GetRequiredService<IIdentifierGenerator<BudgetReservationId>>().ShouldBeSameAs(reservations);
    }

    /// <summary>Verifies conflicting fixed-target evidence is rejected during composition without storage access.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenTargetChanges_ThrowsBeforeStorageAccess()
    {
        var services = new ServiceCollection();
        var settings = JsonBudgetLedgerSettings.CreateDefault();
        _ = services.AddJsonBudgetLedger(
            new JsonBudgetLedgerTarget(
                Path.Combine(TestTemporaryDirectory.Create(), "a"),
                new(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing,
                JsonStoreRecoveryMode.RecoverTornAppends),
            settings);

        _ = Should.Throw<InvalidOperationException>(() => services.AddJsonBudgetLedger(
            new JsonBudgetLedgerTarget(
                Path.Combine(TestTemporaryDirectory.Create(), "b"),
                new(Guid.NewGuid()),
                JsonStoreOpenMode.CreateIfMissing,
                JsonStoreRecoveryMode.RecoverTornAppends),
            settings));
    }

    /// <summary>Verifies conflicting explicit settings evidence is rejected while the target stays the same.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenSettingsChange_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        var target = CreateTarget();
        _ = services.AddJsonBudgetLedger(target, JsonBudgetLedgerSettings.CreateDefault());
        var different = new JsonBudgetLedgerSettings(2_048, 2_048, JsonEncodingSettings.CreateDefault());

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddJsonBudgetLedger(target, different));
        exception.Message.ShouldContain("different settings");
    }

    /// <summary>Verifies more than one directly registered target instance is rejected as an incoherent capture.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenMultipleTargetInstancesAlreadyRegistered_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(CreateTarget());
        _ = services.AddSingleton(CreateTarget());

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddJsonBudgetLedger(CreateTarget(), JsonBudgetLedgerSettings.CreateDefault()));

        exception.Message.ShouldContain(nameof(JsonBudgetLedgerTarget));
    }

    /// <summary>Verifies more than one directly registered settings instance is rejected as an incoherent capture.</summary>
    [Fact]
    public void AddJsonBudgetLedger_WhenMultipleSettingsInstancesAlreadyRegistered_ThrowsInvalidOperation()
    {
        var services = new ServiceCollection();
        _ = services.AddSingleton(JsonBudgetLedgerSettings.CreateDefault());
        _ = services.AddSingleton(new JsonBudgetLedgerSettings(2_048, 2_048, JsonEncodingSettings.CreateDefault()));

        var exception = Should.Throw<InvalidOperationException>(
            () => services.AddJsonBudgetLedger(CreateTarget(), JsonBudgetLedgerSettings.CreateDefault()));

        exception.Message.ShouldContain(nameof(JsonBudgetLedgerSettings));
    }

    private static JsonBudgetLedgerTarget CreateTarget() => new(
        Path.Combine(TestTemporaryDirectory.Create(), "ledger"),
        new(Guid.NewGuid()),
        JsonStoreOpenMode.CreateIfMissing,
        JsonStoreRecoveryMode.RecoverTornAppends);

    private sealed class StubLedger: IBudgetLedger
    {
        public BudgetLedgerDescriptor Descriptor => new(false, BudgetLedgerConcurrencyDomain.ProcessLocal);
        public ValueTask<BudgetLedgerScopeCreateResult> CreateScopeAsync(BudgetLedgerScopeCreateRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerBatchReserveResult> ReserveBatchAsync(BudgetLedgerBatchReserveRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetStartResult> MarkStartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetCommitResult> SettleAsync(BudgetLedgerSettlementRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReleaseResult> ReleaseUnstartedAsync(BudgetLedgerReservationReference reservation, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetCorrectionResult> CorrectAsync(BudgetLedgerCorrectionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetSnapshot> GetSnapshotAsync(BudgetLedgerScopeReference scope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetUnresolvedReservationPage> ReadUnresolvedStartedAsync(BudgetUnresolvedReservationQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetLedgerReconciliationResult> ReconcileAsync(BudgetLedgerReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<BudgetOverrunHoldResolutionResult> ResolveOverrunHoldAsync(BudgetOverrunHoldResolutionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class ScopeIds: IIdentifierGenerator<BudgetScopeId> { public BudgetScopeId Create() => new(Guid.NewGuid()); }
    private sealed class ReservationIds: IIdentifierGenerator<BudgetReservationId> { public BudgetReservationId Create() => new(Guid.NewGuid()); }

    private sealed class EmptyDimensionCatalog: IBudgetDimensionCatalog
    {
        public bool TryGet(BudgetDimension dimension, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out BudgetDimensionDescriptor? descriptor)
        {
            descriptor = null;
            return false;
        }
    }
}
