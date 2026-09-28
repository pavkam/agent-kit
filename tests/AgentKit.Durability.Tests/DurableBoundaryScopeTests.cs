// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Tests;

using AgentKit.IO;

/// <summary>
/// Verifies a boundary scope journals an in-process boundary end to end over the real coordinator, lease manager,
/// and profile catalog, and that it refuses a selected profile the composition cannot honor.
/// </summary>
/// <remarks>
/// This exercises the whole composed path a feature package uses: the scope publishes its continuation, the
/// coordinator acquires a lease and records the start, the shipped boundary handler dispatches back into this
/// process, and the boundary's own mid-operation records reach the selected journal. The first-party
/// <see cref="InputPromotionDurableOperationHandler"/> stands in for any feature boundary here, so a handler that
/// stopped bridging to the registry would fail this test rather than only its own fixture.
/// </remarks>
public sealed class DurableBoundaryScopeTests
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Verifies an unselected profile journals nothing and reports no failure.</summary>
    [Fact]
    public void TryCreate_WhenNoProfileIsSelected_ProducesNoScopeAndNoFailure()
    {
        using var harness = Harness(new DurableBoundaryRegistry());

        var failure = DurableBoundaryScope.TryCreate(
            profileKey: null,
            harness.Coordinator,
            harness.Profiles,
            new DurableBoundaryRegistry(),
            harness.TimeProvider,
            OperationTimeout,
            out var scope);

        failure.ShouldBeNull();
        scope.ShouldBeNull();
    }

    /// <summary>Verifies a selected profile that is not registered fails rather than silently running undurably.</summary>
    [Fact]
    public void TryCreate_WhenTheSelectedProfileIsNotRegistered_ReportsASafeFailure()
    {
        using var harness = Harness(new DurableBoundaryRegistry());

        var failure = DurableBoundaryScope.TryCreate(
            new DurabilityProfileKey("absent"),
            harness.Coordinator,
            harness.Profiles,
            new DurableBoundaryRegistry(),
            harness.TimeProvider,
            OperationTimeout,
            out var scope);

        failure.ShouldBe("The selected durability profile is not registered.");
        scope.ShouldBeNull();
    }

    /// <summary>Verifies a selected profile with no composed runtime fails rather than journaling nothing quietly.</summary>
    [Fact]
    public void TryCreate_WhenDurabilityIsNotComposed_ReportsASafeFailure()
    {
        var failure = DurableBoundaryScope.TryCreate(
            DurabilityRuntimeHarness.ProfileKey,
            coordinator: null,
            profiles: null,
            new DurableBoundaryRegistry(),
            TimeProvider.System,
            OperationTimeout,
            out var scope);

        failure.ShouldBe("A durability profile is selected but no durability runtime is composed.");
        scope.ShouldBeNull();
    }

    /// <summary>Verifies an enabled boundary runs in this process and its checkpoint reaches the selected journal.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTheBoundaryCheckpoints_JournalsStartCheckpointAndTerminal()
    {
        var registry = new DurableBoundaryRegistry();
        using var harness = Harness(registry);
        var scope = Scope(harness, registry);
        var manifest = new DurableInputPromotionManifest(
            DurableJournalTestData.RunId.Value,
            DurableJournalTestData.TurnId.Value,
            "AfterTurnCommitted",
            promotedMessageCount: 0);

        var promoted = await scope.ExecuteAsync(
            IoDurableOperations.InputPromotion,
            IoDurableOperations.InputPromotionVersion,
            DurableJournalTestData.Authorization(),
            DurableBoundaryPayload.Encode(manifest),
            SecurityEffect.Append,
            hooks: null,
            async (context, token) =>
            {
                _ = await context.Checkpoints.RecordCheckpointAsync(
                    DurableCheckpointKind.InputAdmitted, context.Operation.Input, token).ConfigureAwait(false);
                return 2;
            },
            TestContext.Current.CancellationToken);

        promoted.ShouldBe(2);
        harness.Journal.Calls.ShouldBe(["RecordStart", "RecordCheckpoint", "RecordTerminal"]);
        harness.Journal.Checkpoints.ShouldHaveSingleItem()
            .Request.Kind.ShouldBe(DurableCheckpointKind.InputAdmitted);
        harness.Journal.Starts.ShouldHaveSingleItem()
            .Request.Descriptor.Name.ShouldBe(IoDurableOperations.InputPromotion);
    }

    /// <summary>Verifies a boundary waiting on an external owner records that wait with its own certainty.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTheBoundaryWaitsOnAnExternalOwner_JournalsTheWaitingRecord()
    {
        var registry = new DurableBoundaryRegistry();
        using var harness = Harness(registry);
        var scope = Scope(harness, registry);
        var reference = new ExternalOperationReference(
            DurabilityRuntimeHarness.BackendKey, "approval-1");

        var waited = await scope.ExecuteAsync(
            IoDurableOperations.InputPromotion,
            IoDurableOperations.InputPromotionVersion,
            DurableJournalTestData.Authorization(),
            DurableJournalTestData.Payload(),
            SecurityEffect.Observe,
            hooks: null,
            async (context, token) =>
            {
                _ = await context.Checkpoints.RecordWaitingAsync(
                    new DurableWaitCondition(
                        SideEffectCertainty.DefinitelyNotPerformed, reference, notBefore: null),
                    token).ConfigureAwait(false);
                return true;
            },
            TestContext.Current.CancellationToken);

        waited.ShouldBeTrue();
        harness.Journal.Calls.ShouldBe(["RecordStart", "RecordWaiting", "RecordTerminal"]);
        var waiting = harness.Journal.Waits.ShouldHaveSingleItem().Request;
        waiting.ExternalReference.ShouldBe(reference);
        waiting.SideEffectCertainty.ShouldBe(SideEffectCertainty.DefinitelyNotPerformed);
    }

    /// <summary>Verifies the durable address is derived from the boundary's own capture rather than supplied beside it.</summary>
    [Fact]
    public async Task ExecuteAsync_WhenTheCaptureIsBeforeRun_ThrowsForTheAuthorizationArgument()
    {
        var registry = new DurableBoundaryRegistry();
        using var harness = Harness(registry);
        var scope = Scope(harness, registry);
        var captured = DurableJournalTestData.Authorization();
        var beforeRun = new SecurityAuthorizationContext(
            captured.ProfileKey,
            captured.ProfileVersion,
            captured.PolicySnapshot,
            captured.AuthorityKey,
            captured.AgentDefinitionRevision,
            captured.ConfigurationVersion,
            new SecurityAuthorizationScope(
                DurableJournalTestData.AgentId,
                DurableJournalTestData.SessionId,
                new BeforeRunOperationCorrelation(DurableJournalTestData.OperationId, admissionId: null)),
            captured.Identity);

        (await Should.ThrowAsync<ArgumentException>(() => scope.ExecuteAsync(
            IoDurableOperations.InputPromotion,
            IoDurableOperations.InputPromotionVersion,
            beforeRun,
            DurableJournalTestData.Payload(),
            SecurityEffect.Append,
            hooks: null,
            (_, _) => ValueTask.FromResult(true),
            TestContext.Current.CancellationToken).AsTask())).ParamName.ShouldBe("authorization");
    }

    private static DurabilityRuntimeHarness Harness(DurableBoundaryRegistry registry) => new(
        handlers: [new InputPromotionDurableOperationHandler(registry)],
        configureProfile: static options =>
            options.EnabledOperations.Add(IoDurableOperations.InputPromotion));

    private static DurableBoundaryScope Scope(DurabilityRuntimeHarness harness, DurableBoundaryRegistry registry)
    {
        Debug.Assert(harness is not null, "The fixture owns the composed runtime.");
        var failure = DurableBoundaryScope.TryCreate(
            DurabilityRuntimeHarness.ProfileKey,
            harness.Coordinator,
            harness.Profiles,
            registry,
            harness.TimeProvider,
            OperationTimeout,
            out var scope);
        failure.ShouldBeNull();
        return scope.ShouldNotBeNull();
    }
}
