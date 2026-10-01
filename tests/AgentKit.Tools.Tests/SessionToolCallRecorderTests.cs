// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Collections.Immutable;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>Verifies <see cref="SessionToolCallRecorder"/> accepted and terminal recording through a session capability.</summary>
public sealed class SessionToolCallRecorderTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly RunId _runId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly TurnId _turnId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static readonly OperationId _operationId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static readonly BranchId _branchId = new(Guid.Parse("77777777-7777-7777-7777-777777777771"));
    private static readonly ExecutionLaneId _laneId = new(Guid.Parse("77777777-7777-7777-7777-777777777772"));
    private static readonly ToolCallSessionTarget _target = new(_branchId, _laneId);

    [Fact]
    public async Task RecordAcceptedAsync_WhenSessionAcceptsTheAppend_AppendsOneEntryAtTheNextSequenceUnderTheTipVersion()
    {
        var fixture = Fixture.Create();
        var accepted = AcceptedCall(1);

        var result = await fixture.Recorder.RecordAcceptedAsync(accepted, fixture.Session, _target, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolCallRecorded>();
        var request = fixture.Coordinator.AppendRequests.ShouldHaveSingleItem();
        request.BranchId.ShouldBe(_branchId);
        request.ExpectedVersion.ShouldBe(new SessionVersion(0));
        request.IdempotencyKey.ShouldBe(new IdempotencyKey($"agentkit.tool-call:{accepted.CallId}:accepted"));
        request.Context.ExecutionLaneId.ShouldBe(_laneId);
        var entry = request.Entries.ShouldHaveSingleItem().ShouldBeOfType<ToolCallAcceptedSessionEntry>();
        entry.Sequence.ShouldBe(new SessionSequence(1));
        entry.Call.ShouldBeSameAs(accepted);
        entry.BranchId.ShouldBe(_branchId);
        entry.Address.ShouldBe(new SessionAddress(_agentId, _sessionId));
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenBranchAlreadyHoldsEntries_AppendsAfterTheCurrentTip()
    {
        var fixture = Fixture.Create();
        _ = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        _ = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(2), fixture.Session, _target, TestContext.Current.CancellationToken);

        var second = fixture.Coordinator.AppendRequests[1];
        second.ExpectedVersion.ShouldBe(new SessionVersion(1));
        second.Entries[0].Sequence.ShouldBe(new SessionSequence(2));
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenAppendConflicts_RereadsTheTipAndRetriesWithTheSameEntryIdentity()
    {
        var fixture = Fixture.Create();
        var conflicts = 0;
        fixture.Coordinator.Intercept = request =>
        {
            if (conflicts++ > 0)
            {
                return null;
            }

            _ = fixture.Inner.AppendAsync(
                new SessionAppendRequest(request.Context, request.BranchId, request.ExpectedVersion, new IdempotencyKey("other-writer"), [Filler(request, 1)]),
                fixture.Session.Profile).AsTask().GetAwaiter().GetResult();
            return new SessionAppendConflict(request.ExpectedVersion, new SessionVersion(request.ExpectedVersion.Value + 1));
        };

        var result = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolCallRecorded>();
        fixture.Coordinator.AppendRequests.Count.ShouldBe(2);
        fixture.Coordinator.AppendRequests[1].Entries[0].Sequence.ShouldBe(new SessionSequence(2));
        fixture.Coordinator.AppendRequests[1].Entries[0].Id.ShouldBe(fixture.Coordinator.AppendRequests[0].Entries[0].Id);
        fixture.Coordinator.AppendRequests[1].IdempotencyKey.ShouldBe(fixture.Coordinator.AppendRequests[0].IdempotencyKey);
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenConflictPersistsPastTheAttemptBudget_RejectsAsConflict()
    {
        var fixture = Fixture.Create(new ToolRuntimeOptions { MaximumRecordAppendAttempts = 2 });
        fixture.Coordinator.Intercept = static request => new SessionAppendConflict(request.ExpectedVersion, new SessionVersion(request.ExpectedVersion.Value + 1));

        var result = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolCallRecordRejected>().Kind.ShouldBe(ToolCallRecordRejectionKind.Conflict);
        fixture.Coordinator.AppendRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenStoreFailsTheAppend_RejectsAsUnavailable()
    {
        var fixture = Fixture.Create();
        fixture.Coordinator.Intercept = static _ => new SessionAppendFailed("disk detail");

        var result = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<ToolCallRecordRejected>();
        rejected.Kind.ShouldBe(ToolCallRecordRejectionKind.Unavailable);
        rejected.SafeReason.ShouldNotContain("disk detail");
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenBranchCannotBeRead_RejectsAsUnavailableWithoutAppending()
    {
        var fixture = Fixture.Create(seed: false);

        var result = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolCallRecordRejected>().Kind.ShouldBe(ToolCallRecordRejectionKind.Unavailable);
        fixture.Coordinator.AppendRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenCoordinatorThrows_RejectsAsUnavailableWithoutLeakingTheMessage()
    {
        var fixture = Fixture.Create();
        fixture.Coordinator.Intercept = static _ => throw new InvalidOperationException("secret coordinator detail");

        var result = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        var rejected = result.ShouldBeOfType<ToolCallRecordRejected>();
        rejected.Kind.ShouldBe(ToolCallRecordRejectionKind.Unavailable);
        rejected.SafeReason.ShouldNotContain("secret");
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenCallerIsAlreadyCancelled_PropagatesCancellation()
    {
        var fixture = Fixture.Create();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, cancellation.Token));

        fixture.Coordinator.AppendRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenCoordinatorCancels_PropagatesCancellationInsteadOfRejecting()
    {
        var fixture = Fixture.Create();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        fixture.Coordinator.Intercept = _ =>
        {
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        };

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
            await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, cancellation.Token));
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenCallWasAccepted_AppendsContentFreeTerminalEntryParentedToTheAcceptedEntry()
    {
        var fixture = Fixture.Create();
        var accepted = AcceptedCall(1);
        _ = await fixture.Recorder.RecordAcceptedAsync(accepted, fixture.Session, _target, TestContext.Current.CancellationToken);
        var acceptedEntry = fixture.Coordinator.AppendRequests[0].Entries[0];
        var terminal = TerminalFor(accepted, [new ToolResultTextContent("private output", TextSemantics.Plain, ExtensionData.Empty)]);

        var result = await fixture.Recorder.RecordTerminalAsync(terminal, fixture.Session, _target, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolCallRecorded>();
        var entry = fixture.Coordinator.AppendRequests[1].Entries.ShouldHaveSingleItem().ShouldBeOfType<ToolCallTerminalSessionEntry>();
        entry.CausalParentId.ShouldBe(acceptedEntry.Id);
        entry.Result.Content.ShouldBeEmpty();
        entry.Result.Status.ShouldBe(terminal.Status);
        entry.Result.Acceptance.ShouldBe(accepted.Acceptance);
        fixture.Coordinator.AppendRequests[1].IdempotencyKey.ShouldBe(new IdempotencyKey($"agentkit.tool-call:{accepted.CallId}:terminal"));
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenAcceptedRecordIsMissing_RejectsAsConflictWithoutAppending()
    {
        var fixture = Fixture.Create();

        var result = await fixture.Recorder.RecordTerminalAsync(
            TerminalFor(AcceptedCall(1), []), fixture.Session, _target, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolCallRecordRejected>().Kind.ShouldBe(ToolCallRecordRejectionKind.Conflict);
        fixture.Coordinator.AppendRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenTerminalDisagreesWithTheAcceptedRecord_RejectsAsConflict()
    {
        var fixture = Fixture.Create();
        var accepted = AcceptedCall(1);
        _ = await fixture.Recorder.RecordAcceptedAsync(accepted, fixture.Session, _target, TestContext.Current.CancellationToken);
        var mismatched = TerminalFor(accepted, [], admission: new ToolCallAdmissionEvidence(new ToolCatalogVersion("other-catalog"), 0, new InputFingerprint("sha256:raw")));

        var result = await fixture.Recorder.RecordTerminalAsync(mismatched, fixture.Session, _target, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolCallRecordRejected>().Kind.ShouldBe(ToolCallRecordRejectionKind.Conflict);
        fixture.Coordinator.AppendRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenAcceptedEntryIsOlderThanTheLookupWindow_RejectsAsConflict()
    {
        var fixture = Fixture.Create(new ToolRuntimeOptions { AcceptedRecordLookupEntries = 1 });
        var first = AcceptedCall(1);
        _ = await fixture.Recorder.RecordAcceptedAsync(first, fixture.Session, _target, TestContext.Current.CancellationToken);
        _ = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(2), fixture.Session, _target, TestContext.Current.CancellationToken);

        var result = await fixture.Recorder.RecordTerminalAsync(TerminalFor(first, []), fixture.Session, _target, TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolCallRecordRejected>().Kind.ShouldBe(ToolCallRecordRejectionKind.Conflict);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenCallWasRejectedBeforeAcceptance_AppendsWithoutLookingForAnAcceptedRecord()
    {
        var fixture = Fixture.Create();
        var rejected = RejectedBeforeAcceptance(3);

        var result = await fixture.Recorder.RecordTerminalAsync(rejected, fixture.Session, _target, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolCallRecorded>();
        var entry = fixture.Coordinator.AppendRequests.ShouldHaveSingleItem().Entries[0].ShouldBeOfType<ToolCallTerminalSessionEntry>();
        entry.CausalParentId.ShouldBeNull();
        entry.Result.Acceptance.ShouldBeNull();
        fixture.Coordinator.ReadCount.ShouldBe(1);
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenRecording_EmitsContentFreeActivityLogAndMetric()
    {
        var fixture = Fixture.Create();
        var accepted = AcceptedCall(1, alias: "secret-alias-literal");
        var callId = accepted.CallId.ToString();
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ToolCallRecordAccepted
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolCallId), callId));
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolCallRecordCount);

        _ = await fixture.Recorder.RecordAcceptedAsync(accepted, fixture.Session, _target, TestContext.Current.CancellationToken);

        var activity = activities.Snapshot().ShouldHaveSingleItem();
        activity.Status.ShouldBe(ActivityStatusCode.Ok);
        activity.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("recorded");
        activity.GetTagItem(AgentKitTagNames.RunId).ShouldBe(_runId.ToString());
        var logs = fixture.Logger.Snapshot();
        logs.Select(static entry => entry.EventId.Id).ShouldBe([4110, 4111]);
        logs[1].Level.ShouldBe(LogLevel.Debug);
        var ours = metrics.Snapshot().Where(static observation => Equals(observation.Tags[AgentKitTagNames.GenAiOperationName], "accepted")).ToArray();
        ours.ShouldContain(static observation => Equals(observation.Tags[AgentKitTagNames.Outcome], "recorded"));
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logs, ours, "secret-alias-literal", "sha256:validated");
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenRecordingIsRejected_LogsAWarningAndMarksTheActivityFailed()
    {
        var fixture = Fixture.Create();
        fixture.Coordinator.Intercept = static _ => new SessionAppendFailed("x");
        var accepted = AcceptedCall(1);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ToolCallRecordAccepted
                && Equals(observation.GetTagItem(AgentKitTagNames.ToolCallId), accepted.CallId.ToString()));

        _ = await fixture.Recorder.RecordAcceptedAsync(accepted, fixture.Session, _target, TestContext.Current.CancellationToken);

        activities.Snapshot().ShouldHaveSingleItem().Status.ShouldBe(ActivityStatusCode.Error);
        fixture.Logger.Snapshot()[^1].Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenInstrumentationFails_DoesNotChangeTheOutcome()
    {
        var fixture = Fixture.Create();
        fixture.Logger.ThrowOnWrite = true;

        var result = await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, _target, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolCallRecorded>();
    }

    [Fact]
    public async Task RecordAcceptedAsync_WhenArgumentIsNull_ThrowsExactParameterBeforeAnyEffect()
    {
        var fixture = Fixture.Create();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Recorder.RecordAcceptedAsync(null!, fixture.Session, _target))).ParamName.ShouldBe("accepted");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), null!, _target))).ParamName.ShouldBe("session");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Recorder.RecordAcceptedAsync(AcceptedCall(1), fixture.Session, null!))).ParamName.ShouldBe("target");
        fixture.Coordinator.AppendRequests.ShouldBeEmpty();
        fixture.Coordinator.ReadCount.ShouldBe(0);
    }

    [Fact]
    public async Task RecordTerminalAsync_WhenArgumentIsNull_ThrowsExactParameterBeforeAnyEffect()
    {
        var fixture = Fixture.Create();
        var terminal = RejectedBeforeAcceptance(3);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Recorder.RecordTerminalAsync(null!, fixture.Session, _target))).ParamName.ShouldBe("result");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Recorder.RecordTerminalAsync(terminal, null!, _target))).ParamName.ShouldBe("session");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await fixture.Recorder.RecordTerminalAsync(terminal, fixture.Session, null!))).ParamName.ShouldBe("target");
        fixture.Coordinator.ReadCount.ShouldBe(0);
    }

    [Fact]
    public void Constructor_WhenDependencyIsNull_ThrowsExactParameterName()
    {
        var generator = new GuidSessionEntryIds();
        var options = Options.Create(new ToolRuntimeOptions());

        Should.Throw<ArgumentNullException>(() => new SessionToolCallRecorder(null!, options, TimeProvider.System, NullLogger<SessionToolCallRecorder>.Instance)).ParamName.ShouldBe("entryIds");
        Should.Throw<ArgumentNullException>(() => new SessionToolCallRecorder(generator, null!, TimeProvider.System, NullLogger<SessionToolCallRecorder>.Instance)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new SessionToolCallRecorder(generator, options, null!, NullLogger<SessionToolCallRecorder>.Instance)).ParamName.ShouldBe("timeProvider");
        Should.Throw<ArgumentNullException>(() => new SessionToolCallRecorder(generator, options, TimeProvider.System, null!)).ParamName.ShouldBe("logger");
    }

    private static AcceptedToolCall AcceptedCall(int suffix, string alias = "read")
    {
        var correlation = new InRunOperationCorrelation(_operationId, _runId, _turnId);
        var identity = TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
        var authorization = TestSecurityEvidence.Authorization(_agentId, _sessionId, correlation, identity);
        var normalization = ToolRuntimeNormalizationDefaults.ForResolvedTool(
            new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(1)));
        return new AcceptedToolCall(
            _agentId, _sessionId, _runId, _turnId, _operationId, CallId(suffix), authorization,
            new ToolCallAcceptanceEvidence(new GrantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")), new InputFingerprint("sha256:validated"), DateTimeOffset.UnixEpoch),
            new ToolAlias(alias), new ToolId("tool.read"), new ToolVersion("1"), new ToolEffects(ToolEffect.ReadOnly, null, null), externalIdempotencyKey: null,
            new ToolCallAdmissionEvidence(new ToolCatalogVersion("catalog-1"), 0, new InputFingerprint("sha256:raw")),
            normalization, normalization.ProjectionPolicy, DateTimeOffset.UnixEpoch);
    }

    private static ToolCallResult TerminalFor(
        AcceptedToolCall accepted,
        ImmutableArray<ToolResultContent> content,
        ToolCallAdmissionEvidence? admission = null) => new(
        accepted.AgentId, accepted.SessionId, accepted.RunId, accepted.TurnId, accepted.OperationId, accepted.CallId, accepted.Authorization,
        accepted.Acceptance.InvocationGrantId, accepted.Acceptance, accepted.ProviderAlias, accepted.ToolId, accepted.ToolVersion, accepted.Effects,
        accepted.ExternalIdempotencyKey, admission ?? accepted.Admission, ToolTerminalStatus.Succeeded, content, error: null,
        SideEffectCertainty.DefinitelyPerformed, usage: null, retryable: false, accepted.Normalization,
        new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty), accepted.ProjectionPolicy,
        DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, ExtensionData.Empty);

    private static ToolCallResult RejectedBeforeAcceptance(int suffix)
    {
        var accepted = AcceptedCall(suffix);
        var rejection = ToolRuntimeNormalizationDefaults.RejectionSnapshot;
        return new ToolCallResult(
            accepted.AgentId, accepted.SessionId, accepted.RunId, accepted.TurnId, accepted.OperationId, accepted.CallId, accepted.Authorization,
            grantId: null, acceptance: null, accepted.ProviderAlias, toolId: null, toolVersion: null, effects: null, externalIdempotencyKey: null,
            accepted.Admission, ToolTerminalStatus.UnknownTool, [], new ToolError(ToolErrorKind.Tool, "Unknown tool.", null, null, ExtensionData.Empty),
            SideEffectCertainty.DefinitelyNotPerformed, usage: null, retryable: false, rejection,
            new ToolResultNormalizationInfo([], null, null, null, null, ExtensionData.Empty), rejection.ProjectionPolicy,
            DateTimeOffset.UnixEpoch, invocationStartedAt: null, DateTimeOffset.UnixEpoch, ExtensionData.Empty);
    }

    private static ToolCallId CallId(int suffix) => new(Guid.Parse($"66666666-6666-6666-6666-66666666666{suffix}"));

    private static ToolCallTerminalSessionEntry Filler(SessionAppendRequest request, int sequenceOffset) => new ToolCallTerminalSessionEntry(
        new SessionEntryId(Guid.NewGuid()),
        request.Context.ToAddress(),
        (InRunOperationCorrelation) request.Context.Correlation,
        request.BranchId,
        new SessionSequence(request.Entries[0].Sequence.Value + sequenceOffset - 1),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        RejectedBeforeAcceptance(9));

    private sealed class Fixture
    {
        private Fixture()
        {
        }

        public SessionToolCallRecorder Recorder { get; private init; } = null!;
        public ScriptedSessionCoordinator Coordinator { get; private init; } = null!;
        public InMemoryTestSessionCoordinator Inner { get; private init; } = null!;
        public SessionExecutionCapability Session { get; private init; } = null!;
        public RecordingLogger<SessionToolCallRecorder> Logger { get; private init; } = null!;

        public static Fixture Create(ToolRuntimeOptions? options = null, bool seed = true)
        {
            var inner = new InMemoryTestSessionCoordinator();
            if (seed)
            {
                inner.Seed(new SessionDescriptor(
                    new SessionAddress(_agentId, _sessionId), null, new TenantId("tenant"), new PrincipalId("principal"),
                    new SessionStoreKey("test-store"), _branchId, new SessionVersion(0), SessionLifecycleState.Active,
                    DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, new SchemaVersion("1"), ExtensionData.Empty));
            }

            var coordinator = new ScriptedSessionCoordinator(inner);
            var logger = new RecordingLogger<SessionToolCallRecorder>();
            return new Fixture
            {
                Inner = inner,
                Coordinator = coordinator,
                Logger = logger,
                Session = new SessionExecutionCapability(TestSecurityEvidence.SessionProfile(), coordinator, new UnsupportedSessionRunCoordinator()),
                Recorder = new SessionToolCallRecorder(new GuidSessionEntryIds(), Options.Create(options ?? new ToolRuntimeOptions()), TimeProvider.System, logger),
            };
        }
    }

    private sealed class GuidSessionEntryIds: IIdentifierGenerator<SessionEntryId>
    {
        public SessionEntryId Create() => new(Guid.NewGuid());
    }

    private sealed class ScriptedSessionCoordinator(InMemoryTestSessionCoordinator inner): ISessionCoordinator
    {
        public List<SessionAppendRequest> AppendRequests { get; } = [];
        public int ReadCount { get; private set; }
        public Func<SessionAppendRequest, SessionAppendResult?>? Intercept { get; set; }

        public ValueTask<SessionAppendResult> AppendAsync(SessionAppendRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
        {
            AppendRequests.Add(request);
            return Intercept?.Invoke(request) is { } scripted
                ? ValueTask.FromResult(scripted)
                : inner.AppendAsync(request, profile, cancellationToken);
        }

        public ValueTask<SessionPageResult> ReadAsync(SessionReadRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default)
        {
            ReadCount++;
            return inner.ReadAsync(request, profile, cancellationToken);
        }

        public ValueTask<SessionCreateResult> CreateAsync(SessionCreateRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            inner.CreateAsync(request, profile, cancellationToken);

        public ValueTask<SessionLoadResult> LoadAsync(SessionOperationContext context, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            inner.LoadAsync(context, profile, cancellationToken);

        public ValueTask<SessionBranchResult> BranchAsync(SessionBranchRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            inner.BranchAsync(request, profile, cancellationToken);

        public ValueTask<SessionDeleteResult> DeleteAsync(SessionDeleteRequest request, SessionProfileSnapshot profile, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(request, profile, cancellationToken);
    }
}
