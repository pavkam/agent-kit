// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using Microsoft.Extensions.DependencyInjection;

/// <summary>Composes the real durability runtime over controllable adapters for coordinator tests.</summary>
/// <remarks>
/// The harness goes through the package's public registration surface rather than constructing the coordinator
/// directly, so a broken keyed registration fails these tests instead of passing them. Only the persistence targets,
/// clock, and security authority are substituted.
/// </remarks>
internal sealed class DurabilityRuntimeHarness: IDisposable
{
    private readonly ServiceProvider _provider;

    /// <summary>Builds one composition selecting the supplied adapters under the shared test keys.</summary>
    /// <param name="journal">The journal the profile selects, or null for a fresh recording journal.</param>
    /// <param name="handlers">The operation handlers to register, or null for one default recording handler.</param>
    /// <param name="backend">The backend the profile selects, or null for the process-local backend.</param>
    /// <param name="configure">An optional callback refining the engine-wide options.</param>
    /// <param name="logger">The logger the coordinator resolves, or null for a discarding logger.</param>
    /// <param name="configureProfile">
    /// An optional callback refining the single registered profile after its component keys are selected, for
    /// example to enable the operation names a boundary scope requires.
    /// </param>
    internal DurabilityRuntimeHarness(
        RecordingDurableOperationJournal? journal = null,
        IReadOnlyList<IDurableOperationHandler>? handlers = null,
        IDurableExecutionBackend? backend = null,
        Action<AgentDurabilityOptions>? configure = null,
        ILogger<DurableExecutionCoordinator>? logger = null,
        Action<DurabilityProfileOptions>? configureProfile = null)
    {
        Journal = journal ?? new RecordingDurableOperationJournal();
        Handler = handlers is null ? new RecordingDurableOperationHandler() : null;
        Backend = backend ?? new InMemoryDurableExecutionBackend(BackendKey);
        TimeProvider = new FakeTimeProvider(DurableJournalTestData.Now);
        Authority = new AllowingSecurityAuthority();
        LeaseManager = new InMemoryDurableLeaseManager(
            new SequentialExecutionLeaseIdGenerator(), TimeProvider, NullLogger<InMemoryDurableLeaseManager>.Instance);

        var services = new ServiceCollection();
        _ = services.AddLogging();
        if (logger is not null)
        {
            _ = services.AddSingleton(logger);
        }

        _ = services.AddSingleton<TimeProvider>(TimeProvider);
        _ = services.AddSingleton<ISecurityAuthoritySelector>(new FixedSecurityAuthoritySelector(Authority));
        _ = services.AddAgentDurability(options =>
        {
            options.LeaseDuration = TimeSpan.FromMinutes(1);
            options.LeaseRenewalInterval = TimeSpan.FromSeconds(20);
            configure?.Invoke(options);
        });
        _ = services.AddDurabilityProfile(ProfileKey, options =>
        {
            options.BackendKey = BackendKey;
            options.JournalKey = JournalKey;
            options.LeaseManagerKey = LeaseManagerKey;
            options.RecoveryPolicyKey = RecoveryPolicyKey;
            configureProfile?.Invoke(options);
        });
        _ = services.AddKeyedSingleton<IDurableOperationJournal>(JournalKey.Value, Journal);
        _ = services.AddKeyedSingleton<IDurableLeaseManager>(LeaseManagerKey.Value, LeaseManager);
        _ = services.AddKeyedSingleton(BackendKey.Value, Backend);
        _ = services.AddRecoveryPolicy<DefaultRecoveryPolicy>(RecoveryPolicyKey);
        foreach (var handler in handlers ?? [Handler!])
        {
            _ = services.AddSingleton(handler);
        }

        _provider = services.BuildServiceProvider();
    }

    /// <summary>Gets the profile key every harness context selects.</summary>
    /// <value>The single profile registered by this harness.</value>
    internal static DurabilityProfileKey ProfileKey { get; } = new("profile");

    /// <summary>Gets the backend key every harness context selects.</summary>
    /// <value>The single backend registered by this harness.</value>
    internal static DurableBackendKey BackendKey { get; } = new("backend");

    /// <summary>Gets the journal key every harness context selects.</summary>
    /// <value>The single journal registered by this harness.</value>
    internal static DurableJournalKey JournalKey { get; } = new("journal");

    /// <summary>Gets the lease-manager key every harness context selects.</summary>
    /// <value>The single lease manager registered by this harness.</value>
    internal static DurableLeaseManagerKey LeaseManagerKey { get; } = new("leases");

    /// <summary>Gets the recovery-policy key every harness context selects.</summary>
    /// <value>The single recovery policy registered by this harness.</value>
    internal static RecoveryPolicyKey RecoveryPolicyKey { get; } = new("policy");

    /// <summary>Gets the journal the composed profile selects.</summary>
    /// <value>The recording journal observing every authorized write.</value>
    internal RecordingDurableOperationJournal Journal { get; }

    /// <summary>Gets the default handler, when the harness created one.</summary>
    /// <value>The single recording handler, or null when explicit handlers were supplied.</value>
    internal RecordingDurableOperationHandler? Handler { get; }

    /// <summary>Gets the backend the composed profile selects.</summary>
    /// <value>The registered backend instance.</value>
    internal IDurableExecutionBackend Backend { get; }

    /// <summary>Gets the lease manager the composed profile selects.</summary>
    /// <value>The process-local lease manager allocating fencing generations.</value>
    internal InMemoryDurableLeaseManager LeaseManager { get; }

    /// <summary>Gets the controllable clock the runtime uses for renewal and record instants.</summary>
    /// <value>A fake clock the test advances explicitly.</value>
    internal FakeTimeProvider TimeProvider { get; }

    /// <summary>Gets the authority that issued every grant the coordinator presented.</summary>
    /// <value>The recording authority.</value>
    internal AllowingSecurityAuthority Authority { get; }

    /// <summary>Gets the composed coordinator under test.</summary>
    /// <value>The singular registered coordinator.</value>
    internal IDurableExecutionCoordinator Coordinator =>
        _provider.GetRequiredService<IDurableExecutionCoordinator>();

    /// <summary>Gets the composed profile catalog naming this harness's single registered profile.</summary>
    /// <value>The singular registered catalog, so a boundary scope resolves the same snapshot the coordinator does.</value>
    internal IDurabilityProfileCatalog Profiles =>
        _provider.GetRequiredService<IDurabilityProfileCatalog>();

    /// <summary>Gets a durable execution context naming this harness's registrations.</summary>
    /// <value>The captured composition every operation in these tests runs under.</value>
    internal static DurableExecutionContext Context(OperationId? operationId = null) =>
        DurableJournalTestData.Context(operationId: operationId);

    /// <inheritdoc/>
    public void Dispose() => _provider.Dispose();
}
