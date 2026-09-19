// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Input;



/// <summary>Verifies InputPromoted behavior and contracts.</summary>
public sealed class InputPromotedTests
{
    [Fact]
    public void InputPromoted_WhenRecordsDoNotMatchSnapshotOrder_ThrowsArgumentExceptionWithParamName()
    {
        var first = Admitted(Payload(1, InputDelivery.Steer), Payload(1, InputDelivery.Steer), new SessionSequence(3));
        var secondPayload = Payload(2, InputDelivery.Steer);
        var second = new AdmittedInput(Admission(2), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), secondPayload, secondPayload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(4));
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(Snapshot([first.AdmissionId, second.AdmissionId]), [second, first], new SessionVersion(1), CommittedCursor(), Revision()));
        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void InputPromoted_WhenPromotedCountDiffersFromSnapshotSelection_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var snapshot = Snapshot([Admission(1), Admission(2)]);
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(snapshot, [promoted], new SessionVersion(1), CommittedCursor(), Revision()));
        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void InputPromoted_WhenRecordTargetsAnotherLane_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), OtherLane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(Snapshot([promoted.AdmissionId]), [promoted], new SessionVersion(1), CommittedCursor(), Revision()));
        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void InputPromoted_WhenRecordWasAdmittedAfterSnapshotCutoff_ThrowsArgumentExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var snapshot = new InputPromotionSnapshot(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(0), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), [Admission(1)]);
        var exception = ShouldThrowExactly<ArgumentException>(() => new InputPromoted(snapshot, [promoted], new SessionVersion(1), CommittedCursor(), Revision()));
        exception.ParamName.ShouldBe("promoted");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var snapshot = Snapshot([Admission(1)]);
        var version = new SessionVersion(1);
        var committedCursor = CommittedCursor();
        var revision = Revision();
        var result = new InputPromoted(snapshot, [promoted], version, committedCursor, revision);
        result.Snapshot.ShouldBeSameAs(snapshot);
        result.Promoted.ShouldBe([promoted]);
        result.SessionVersion.ShouldBe(version);
        result.CommittedCursor.ShouldBeSameAs(committedCursor);
        result.OperationStateRevision.ShouldBe(revision);
        InputPromotionResult typed = result;
        _ = typed.ShouldBeOfType<InputPromoted>();
    }

    [Fact]
    public void Constructor_WhenCommittedCursorIsNull_ThrowsArgumentNullExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var exception = Should.Throw<ArgumentNullException>(
            () => new InputPromoted(Snapshot([Admission(1)]), [promoted], new SessionVersion(1), null!, Revision()));
        exception.ParamName.ShouldBe("committedCursor");
    }

    [Fact]
    public void Constructor_WhenOperationStateRevisionIsDefault_ThrowsArgumentOutOfRangeExceptionWithParamName()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var exception = Should.Throw<ArgumentOutOfRangeException>(
            () => new InputPromoted(Snapshot([Admission(1)]), [promoted], new SessionVersion(1), CommittedCursor(), default));
        exception.ParamName.ShouldBe("operationStateRevision");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var payload = Payload(1, InputDelivery.Steer);
        var promoted = new AdmittedInput(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), payload, payload, Manifest(), DateTimeOffset.UnixEpoch, new SessionSequence(2));
        var original = new InputPromoted(Snapshot([Admission(1)]), [promoted], new SessionVersion(1), CommittedCursor(), Revision());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static AdmittedInput Admitted(AgentInput original, AgentInput effective, SessionSequence? promoted = null) => new(Admission(1), Agent(), Session(), Lane(), Identity(), new SessionSequence(1), original, effective, Manifest(), DateTimeOffset.UnixEpoch, promoted);
    private static InputPromotionSnapshot Snapshot(ImmutableArray<AdmissionId> admissions) => new(Agent(), Session(), Lane(), Operation(), new OperationStateRevision(1), new SessionBranchCursor(Branch(), null), new SessionSequence(1), null, null, PromotionBoundary.AfterTurnCommitted, Turn(), NextTurn(), admissions);
    private static AgentInput Payload(long id, InputDelivery delivery) => new(Input(id), delivery, [Part()], ExtensionData.Empty);
    private static TextPart Part() => new("input", TextSemantics.Plain, ExtensionData.Empty);
    private static InputPreprocessingManifest Manifest() => new(new ConfigurationVersion(1), new InputFingerprint("original:1"), new InputFingerprint("effective:1"));
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human);
    private static ExecutionLaneId Lane() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static ExecutionLaneId OtherLane() => new(Guid.Parse("40000000-0000-0000-0000-000000000002"));
    private static AdmissionId Admission(long value) => new(Guid.Parse($"00000000-0000-0000-0000-{value:000000000000}"));
    private static InputId Input(long value) => new(Guid.Parse($"10000000-0000-0000-0000-{value:000000000000}"));
    private static AgentId Agent() => new(Guid.Parse("20000000-0000-0000-0000-000000000001"));
    private static SessionId Session() => new(Guid.Parse("30000000-0000-0000-0000-000000000001"));
    private static BranchId Branch() => new(Guid.Parse("40000000-0000-0000-0000-000000000001"));
    private static TurnId Turn() => new(Guid.Parse("50000000-0000-0000-0000-000000000001"));
    private static TurnId NextTurn() => new(Guid.Parse("50000000-0000-0000-0000-000000000002"));
    private static InRunOperationCorrelation Operation() => new(new OperationId(Guid.Parse("60000000-0000-0000-0000-000000000001")), new RunId(Guid.Parse("70000000-0000-0000-0000-000000000001")), Turn());
    private static SessionBranchCursor CommittedCursor() => new(Branch(), null);
    private static OperationStateRevision Revision() => new(2);

    private static TException ShouldThrowExactly<TException>(Func<object?> action)
        where TException : Exception
    {
        var exception = Should.Throw<Exception>(() => _ = action());
        exception.GetType().ShouldBe(typeof(TException));
        return (TException) exception;
    }
}
