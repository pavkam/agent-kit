// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

/// <summary>Verifies that selecting a durability profile forces its whole runtime path to exist at composition.</summary>
public sealed class DurabilityCompositionValidatorTests
{
    private static readonly DurabilityProfileKey Profile = new("durable");

    [Fact]
    public void Validate_WhenNoDefinitionSelectsDurability_ReportsNothing()
    {
        // Durable execution is optional: a composition with no durable agent needs no durability registration.
        var diagnostics = Validate(Definitions(durable: false), profileCatalog: null, Registrations(complete: false));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenTheCompositionIsComplete_ReportsNothing()
    {
        var diagnostics = Validate(Definitions(durable: true), Catalog(), Registrations(complete: true));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenNoProfileCatalogIsRegistered_ReportsTheMissingProfile()
    {
        var diagnostics = Validate(Definitions(durable: true), profileCatalog: null, Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.durability.profile.missing");
    }

    [Fact]
    public void Validate_WhenTheSelectedProfileIsNotRegistered_ReportsTheMissingProfile()
    {
        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(new DurabilityProfileKey("other")),
            Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldContain("agentkit.durability.profile.missing");
    }

    [Fact]
    public void Validate_WhenTheProfileIsMissing_DoesNotAlsoReportItsComponentKeys()
    {
        // Without a resolved profile there are no component keys to check, so further diagnostics would be noise.
        var diagnostics = Validate(Definitions(durable: true), profileCatalog: null, Registrations(complete: true));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.durability.profile.missing"]);
    }

    [Theory]
    [InlineData(typeof(IDurableExecutionCoordinator), "agentkit.durability.coordinator.missing")]
    [InlineData(typeof(IDurabilityRuntimeSelector), "agentkit.durability.runtime-selector.missing")]
    [InlineData(typeof(IDurableBackendCatalog), "agentkit.durability.backend-catalog.missing")]
    [InlineData(typeof(IDurableExecutionEventDispatcher), "agentkit.durability.event-dispatcher.missing")]
    [InlineData(typeof(IIdentifierGenerator<CheckpointId>), "agentkit.durability.checkpointid.missing")]
    [InlineData(typeof(IIdentifierGenerator<WorkerId>), "agentkit.durability.workerid.missing")]
    public void Validate_WhenASingularRuntimeServiceIsMissing_ReportsThatExactDiagnostic(
        Type omitted,
        string expectedCode)
    {
        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(),
            Registrations(complete: true, omitUnkeyed: omitted));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe([expectedCode]);
    }

    [Theory]
    [InlineData("journal", "agentkit.durability.journal.missing")]
    [InlineData("leases", "agentkit.durability.lease-manager.missing")]
    [InlineData("policy", "agentkit.durability.recovery-policy.missing")]
    [InlineData("backend", "agentkit.durability.backend.missing")]
    public void Validate_WhenAKeyedComponentIsMissing_ReportsThatExactDiagnostic(string omitted, string expectedCode)
    {
        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(),
            Registrations(complete: true, omitKeyed: omitted));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe([expectedCode]);
    }

    [Fact]
    public void Validate_WhenAComponentIsRegisteredUnkeyed_StillRequiresTheProfilesExactKey()
    {
        // Recovery activates the exact key the operation persisted; an unkeyed fallback would resolve a different one.
        var services = Services(complete: true, omitKeyed: "journal");
        _ = services.AddSingleton<IDurableOperationJournal>(new StubDurableOperationJournal());

        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(),
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.durability.journal.missing"]);
    }

    [Fact]
    public void Validate_WhenAComponentIsKeyedToAnotherProfilesKey_ReportsTheMissingSelection()
    {
        var services = Services(complete: true, omitKeyed: "journal");
        _ = services.AddKeyedSingleton<IDurableOperationJournal>(
            "elsewhere",
            new StubDurableOperationJournal());

        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(),
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(["agentkit.durability.journal.missing"]);
    }

    [Fact]
    public void Validate_WhenNothingIsRegistered_ReportsEveryGapExactlyOnce()
    {
        var diagnostics = Validate(Definitions(durable: true), Catalog(), Registrations(complete: false));

        diagnostics.Select(static diagnostic => diagnostic.Code).ShouldBe(
            [
                "agentkit.durability.coordinator.missing",
                "agentkit.durability.runtime-selector.missing",
                "agentkit.durability.backend-catalog.missing",
                "agentkit.durability.event-dispatcher.missing",
                "agentkit.durability.checkpointid.missing",
                "agentkit.durability.workerid.missing",
                "agentkit.durability.journal.missing",
                "agentkit.durability.lease-manager.missing",
                "agentkit.durability.recovery-policy.missing",
                "agentkit.durability.backend.missing",
            ]);
    }

    [Fact]
    public void Validate_WhenTwoDefinitionsSelectDurability_ReportsTheSingularGapsOnlyOnce()
    {
        var first = CompositionTestData.Definition(new AgentId(Guid.Parse("c0000000-0000-0000-0000-000000000001")))
            with
        { OptionalCapabilities = Selection(Profile) };
        var second = CompositionTestData.Definition(new AgentId(Guid.Parse("c0000000-0000-0000-0000-000000000002")))
            with
        { OptionalCapabilities = Selection(Profile) };

        var diagnostics = Validate(
            [first, second],
            Catalog(),
            Registrations(complete: true, omitUnkeyed: typeof(IDurableExecutionCoordinator)));

        diagnostics.Select(static diagnostic => diagnostic.Code)
            .ShouldBe(["agentkit.durability.coordinator.missing"]);
    }

    [Fact]
    public void Validate_WhenAnEnabledOperationHasNoHandler_ReportsThatOperationByName()
    {
        // A profile that lists a boundary promises evidence for it; without a handler the coordinator refuses it
        // mid-run, so composition must report the gap instead.
        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(Profile, EngineDurableOperations.RunAdmission, IoDurableOperations.RunSettlement),
            Registrations(complete: true),
            new StubDurableOperationHandler(EngineDurableOperations.RunAdmission));

        var missing = diagnostics.ShouldHaveSingleItem();
        missing.Code.ShouldBe("agentkit.durability.handler.missing");
        missing.SafeMessage.ShouldContain(IoDurableOperations.RunSettlement.Value);
        missing.SafeMessage.ShouldNotContain(EngineDurableOperations.RunAdmission.Value);
    }

    [Fact]
    public void Validate_WhenEveryEnabledOperationHasAHandler_ReportsNothing()
    {
        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(Profile, EngineDurableOperations.RunAdmission, IoDurableOperations.RunSettlement),
            Registrations(complete: true),
            new StubDurableOperationHandler(EngineDurableOperations.RunAdmission),
            new StubDurableOperationHandler(IoDurableOperations.RunSettlement));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenAProfileEnablesNoOperation_NeedsNoHandlers()
    {
        var diagnostics = Validate(Definitions(durable: true), Catalog(), Registrations(complete: true));

        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Validate_WhenAProfileIsSelected_NeverActivatesAPersistenceAdapter()
    {
        // Activating a journal during validation would perform the protected effects composition has not yet proven.
        var activations = 0;
        var services = Services(complete: true, omitKeyed: "journal");
        _ = services.AddKeyedSingleton<IDurableOperationJournal>(
            "journal",
            (_, _) =>
            {
                activations++;
                throw new InvalidOperationException("Composition validation must not activate the journal.");
            });

        var diagnostics = Validate(
            Definitions(durable: true),
            Catalog(),
            ComponentRegistrationSnapshot.Capture(services));

        diagnostics.ShouldBeEmpty();
        activations.ShouldBe(0);
    }

    private static ImmutableArray<CompositionDiagnostic> Validate(
        ImmutableArray<AgentDefinition> definitions,
        IDurabilityProfileCatalog? profileCatalog,
        ComponentRegistrationSnapshot registrations,
        params IDurableOperationHandler[] handlers)
    {
        var diagnostics = ImmutableArray.CreateBuilder<CompositionDiagnostic>();
        DurabilityCompositionValidator.Validate(definitions, profileCatalog, registrations, handlers, diagnostics);
        return diagnostics.ToImmutable();
    }

    private static ImmutableArray<AgentDefinition> Definitions(bool durable) =>
        [
            durable
                ? CompositionTestData.Definition() with { OptionalCapabilities = Selection(Profile) }
                : CompositionTestData.Definition(),
        ];

    private static AgentOptionalCapabilitySelection Selection(DurabilityProfileKey profile) =>
        new(null, null, profile, null, null, []);

    private static StubDurabilityProfileCatalog Catalog(
        DurabilityProfileKey? registered = null,
        params DurableOperationName[] enabledOperations) =>
        new StubDurabilityProfileCatalog(registered ?? Profile, enabledOperations);

    private static ComponentRegistrationSnapshot Registrations(
        bool complete,
        Type? omitUnkeyed = null,
        string? omitKeyed = null) =>
        ComponentRegistrationSnapshot.Capture(Services(complete, omitUnkeyed, omitKeyed));

    private static ServiceCollection Services(bool complete, Type? omitUnkeyed = null, string? omitKeyed = null)
    {
        var services = new ServiceCollection();
        if (!complete)
        {
            return services;
        }

        if (omitUnkeyed != typeof(IDurableExecutionCoordinator))
        {
            _ = services.AddSingleton<IDurableExecutionCoordinator>(new StubDurableExecutionCoordinator());
        }

        if (omitUnkeyed != typeof(IDurabilityRuntimeSelector))
        {
            _ = services.AddSingleton<IDurabilityRuntimeSelector>(new StubDurabilityRuntimeSelector());
        }

        if (omitUnkeyed != typeof(IDurableBackendCatalog))
        {
            _ = services.AddSingleton<IDurableBackendCatalog>(new StubDurableBackendCatalog());
        }

        if (omitUnkeyed != typeof(IDurableExecutionEventDispatcher))
        {
            _ = services.AddSingleton<IDurableExecutionEventDispatcher>(new StubDurableExecutionEventDispatcher());
        }

        if (omitUnkeyed != typeof(IIdentifierGenerator<CheckpointId>))
        {
            _ = services.AddSingleton<IIdentifierGenerator<CheckpointId>>(
                new DelegateIdentifierGenerator<CheckpointId>(static () => new CheckpointId(Guid.NewGuid())));
        }

        if (omitUnkeyed != typeof(IIdentifierGenerator<WorkerId>))
        {
            _ = services.AddSingleton<IIdentifierGenerator<WorkerId>>(
                new DelegateIdentifierGenerator<WorkerId>(static () => new WorkerId(Guid.NewGuid())));
        }

        if (omitKeyed != "journal")
        {
            _ = services.AddKeyedSingleton<IDurableOperationJournal>("journal", new StubDurableOperationJournal());
        }

        if (omitKeyed != "leases")
        {
            _ = services.AddKeyedSingleton<IDurableLeaseManager>("leases", new StubDurableLeaseManager());
        }

        if (omitKeyed != "policy")
        {
            _ = services.AddKeyedSingleton<IRecoveryPolicy>("policy", new StubRecoveryPolicy());
        }

        if (omitKeyed != "backend")
        {
            _ = services.AddKeyedSingleton<IDurableExecutionBackend>("backend", new StubDurableExecutionBackend());
        }

        return services;
    }

    /// <summary>A catalog that publishes exactly one profile under the key the test registered.</summary>
    private sealed class StubDurabilityProfileCatalog(
        DurabilityProfileKey registered,
        DurableOperationName[] enabledOperations): IDurabilityProfileCatalog
    {
        public bool TryGet(DurabilityProfileKey key, [NotNullWhen(true)] out DurabilityProfileSnapshot? profile)
        {
            profile = key == registered
                ? new DurabilityProfileSnapshot(
                    registered,
                    new DurabilityProfileVersion(1),
                    new DurableBackendKey("backend"),
                    new DurableJournalKey("journal"),
                    new DurableLeaseManagerKey("leases"),
                    new RecoveryPolicyKey("policy"),
                    [.. enabledOperations],
                    new ContentHash("sha256:profile"))
                : null;
            return profile is not null;
        }
    }

    private sealed class StubDurableOperationHandler(DurableOperationName operationName): IDurableOperationHandler
    {
        public DurableOperationName OperationName { get; } = operationName;

        public ValueTask<DurableOperationResult> InvokeAsync(
            DurableInvocationContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never invokes a handler.");
    }

    private sealed class StubDurableExecutionCoordinator: IDurableExecutionCoordinator
    {
        public Task<DurableOperationResult> ExecuteAsync(
            RecoverableOperationDescriptor operation,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never runs a durable operation.");

        public Task<DurableOperationResult> RecoverAsync(
            DurableOperationAddress address,
            DurableExecutionContext context,
            HookDispatchContext? hooks,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never recovers a durable operation.");
    }

    private sealed class StubDurabilityRuntimeSelector: IDurabilityRuntimeSelector
    {
        public ValueTask<DurabilityRuntimeActivationResult> ActivateAsync(
            DurableExecutionContext context,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never activates a durability runtime.");
    }

    private sealed class StubDurableBackendCatalog: IDurableBackendCatalog
    {
        public ImmutableArray<DurableBackendDescriptor> GetDescriptors() => [];
    }

    private sealed class StubDurableExecutionEventDispatcher: IDurableExecutionEventDispatcher
    {
        public ValueTask PublishAsync(
            DurableExecutionContext context,
            DurableExecutionEvent executionEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never publishes a durable event.");
    }

    private sealed class StubDurableOperationJournal: IDurableOperationJournal
    {
        public ComponentId SecurityAudience { get; } = new("test.journal");

        public ValueTask<DurableRecordResult> RecordStartAsync(
            AuthorizedDurableRequest<DurableOperationStart> start,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never writes a durable record.");

        public ValueTask<DurableRecordResult> RecordCheckpointAsync(
            AuthorizedDurableRequest<DurableCheckpoint> checkpoint,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never writes a durable record.");

        public ValueTask<DurableRecordResult> RecordTerminalAsync(
            AuthorizedDurableRequest<DurableOperationResult> result,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never writes a durable record.");

        public ValueTask<DurableRecordResult> RecordWaitingAsync(
            AuthorizedDurableRequest<DurableOperationWaiting> waiting,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never writes a durable record.");

        public ValueTask<RecoveryEvidenceResult> LoadEvidenceAsync(
            AuthorizedDurableRequest<DurableOperationAddress> address,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never reads durable evidence.");
    }

    private sealed class StubDurableLeaseManager: IDurableLeaseManager
    {
        public ValueTask<ExecutionLeaseResult> AcquireAsync(
            ExecutionLeaseRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never acquires ownership.");
    }

    private sealed class StubRecoveryPolicy: IRecoveryPolicy
    {
        public ValueTask<RecoveryDecision> DecideAsync(
            RecoverableOperationDescriptor operation,
            RecoveryEvidence evidence,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never classifies recovery.");
    }

    private sealed class StubDurableExecutionBackend: IDurableExecutionBackend
    {
        public DurableBackendDescriptor Descriptor { get; } = new(
            new DurableBackendKey("backend"),
            new DurableBackendCapabilities(false, false, false),
            [],
            supportsFencing: false,
            supportsReconciliation: false);

        public ValueTask<DurableDispatchResult> DispatchAsync(
            DurableDispatchRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never dispatches.");

        public ValueTask<DurableReconciliationResult> ReconcileAsync(
            DurableReconciliationRequest request,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Composition validation never reconciles.");
    }
}
