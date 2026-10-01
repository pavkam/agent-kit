// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ToolExecutionPolicySelector"/> exact-reference selection.</summary>
public sealed class ToolExecutionPolicySelectorTests
{
    private static readonly ToolExecutionPolicyReference _newer = new(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(2));

    [Fact]
    public async Task SelectAsync_WhenReferenceIsRegisteredAndBound_SelectsThePolicy()
    {
        var policy = Policy(Standard);
        var selector = Selector(logger: null, (Standard, policy));

        var result = await selector.SelectAsync(Standard, Capability(Standard), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolExecutionPolicySelected>().Policy.ShouldBeSameAs(policy);
    }

    [Fact]
    public async Task SelectAsync_WhenOnlyANewerRevisionIsRegistered_ReturnsUnavailableForTheExactReference()
    {
        var selector = Selector(logger: null, (_newer, Policy(_newer)));

        var result = await selector.SelectAsync(Standard, Capability(Standard, _newer), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolExecutionPolicyUnavailable>().Reference.ShouldBe(Standard);
    }

    [Fact]
    public async Task SelectAsync_WhenReferenceIsRegisteredButNotBoundToTheRun_ReturnsUnavailable()
    {
        var selector = Selector(logger: null, (Standard, Policy(Standard)));

        var result = await selector.SelectAsync(Standard, Capability(_newer), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolExecutionPolicyUnavailable>();
    }

    [Fact]
    public async Task SelectAsync_WhenSelectionHappens_LogsSelectedAndUnavailableEventsWithoutPolicyContent()
    {
        var logger = new RecordingLogger<ToolExecutionPolicySelector>();
        var selector = Selector(logger, (Standard, Policy(Standard)));

        _ = await selector.SelectAsync(Standard, Capability(Standard), TestContext.Current.CancellationToken);
        _ = await selector.SelectAsync(_newer, Capability(_newer), TestContext.Current.CancellationToken);

        var entries = logger.Snapshot();
        entries.Select(static entry => entry.EventId.Id).ShouldBe([4121, 4120]);
        entries[1].Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task SelectAsync_WhenInstrumentationFails_DoesNotChangeTheSelection()
    {
        var logger = new RecordingLogger<ToolExecutionPolicySelector> { ThrowOnWrite = true };
        var selector = Selector(logger, (Standard, Policy(Standard)));

        var result = await selector.SelectAsync(Standard, Capability(Standard), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolExecutionPolicySelected>();
    }

    [Fact]
    public async Task SelectAsync_WhenArgumentsAreInvalid_ThrowsExactParameterAndHonorsCancellation()
    {
        var selector = Selector(logger: null, (Standard, Policy(Standard)));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await selector.SelectAsync(null!, Capability(Standard)))).ParamName.ShouldBe("reference");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await selector.SelectAsync(Standard, null!))).ParamName.ShouldBe("capability");
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await selector.SelectAsync(Standard, Capability(Standard), cancellation.Token));
    }

    [Fact]
    public void Constructor_WhenPolicyReferenceDiffersFromItsKey_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() =>
            Selector(logger: null, (Standard, Policy(_newer))));

        exception.ParamName.ShouldBe("policies");
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameter()
    {
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPolicySelector(null!, NullLogger<ToolExecutionPolicySelector>.Instance)).ParamName.ShouldBe("policies");
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPolicySelector(new Dictionary<ToolExecutionPolicyReference, IToolExecutionPolicy>(), null!)).ParamName.ShouldBe("logger");
    }

    private static DefaultToolExecutionPolicy Policy(ToolExecutionPolicyReference reference) =>
        new(reference, Options.Create(new ToolRuntimeOptions()));

    private static ToolExecutionPolicySelector Selector(
        ILogger<ToolExecutionPolicySelector>? logger,
        params (ToolExecutionPolicyReference Key, IToolExecutionPolicy Policy)[] policies) =>
        new(
            policies.ToDictionary(static pair => pair.Key, static pair => pair.Policy),
            logger ?? NullLogger<ToolExecutionPolicySelector>.Instance);

    private static ToolExecutionCapability Capability(params ToolExecutionPolicyReference[] bound) => new(
        new SessionExecutionCapability(TestSecurityEvidence.SessionProfile(), new UnsupportedSessionCoordinator(), new UnsupportedSessionRunCoordinator()),
        new BudgetExecutionCapability(
            new BudgetProfileKey("standard"), new BudgetProfileVersion(1),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human),
            new InRunOperationCorrelation(OperationId, RunId, TurnId), new NeverScope()),
        new ToolCallSessionTarget(new BranchId(Guid.NewGuid()), null),
        [.. bound.Select(static reference => new ToolExecutionPolicyBinding(reference))]);

    private sealed class NeverScope: IBudgetScope
    {
        public BudgetScopeId Id { get; } = new(Guid.NewGuid());
        public BudgetScopeAddress Address { get; } = new(new TenantId("tenant"), new PrincipalId("principal"), AgentId, SessionId, RunId, OperationId);

        public ValueTask<BudgetReservationResult> ReserveAsync(BudgetReservationRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetBatchReservationResult> ReserveBatchAsync(ImmutableArray<BudgetReservationRequest> requests, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<BudgetSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
