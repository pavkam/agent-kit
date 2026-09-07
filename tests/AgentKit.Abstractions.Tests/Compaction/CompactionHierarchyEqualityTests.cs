// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>
/// Exercises the structural equality and remaining constructor guards of
/// the context-compaction request/result/hierarchy records that are
/// otherwise only ever constructed once and compared by type (never for
/// value equality) by <c>AgentKit.Context.Compaction.Tests</c>.
/// </summary>
public sealed class CompactionHierarchyEqualityTests
{
    [Fact]
    public void CompactionCandidate_Equality_WhenSameValues_InstancesAreEqual() =>
        Candidate().ShouldBe(Candidate());

    [Fact]
    public void CompactionCut_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Cut();
        var second = Cut();

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void CompactionCut_Equality_WhenCoveredEntryIdsDiffer_InstancesAreNotEqual()
    {
        var first = Cut();
        var second = new CompactionCut(
            Range(),
            new SessionSequence(3),
            [new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))]);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void CompactionManifest_Equality_WhenSameValues_InstancesAreEqual() =>
        Manifest().ShouldBe(Manifest());

    [Fact]
    public void CompactionOperationContext_Equality_WhenSameValues_InstancesAreEqual() =>
        Context().ShouldBe(Context());

    [Fact]
    public void CompactionProducer_Equality_WhenSameValues_InstancesAreEqual() =>
        Producer().ShouldBe(Producer());

    [Fact]
    public void CompactionRejection_Equality_WhenSameValues_InstancesAreEqual() =>
        Rejection().ShouldBe(Rejection());

    [Fact]
    public void CompactionFailure_Equality_WhenSameValues_InstancesAreEqual() =>
        Failure().ShouldBe(Failure());

    [Fact]
    public void CompactionSizeEstimate_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionSizeEstimate(1, 2, 3).ShouldBe(new CompactionSizeEstimate(1, 2, 3));

    [Fact]
    public void CompactionSourceRange_Constructor_WhenEndPrecedesStart_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new CompactionSourceRange(new SessionSequence(5), new SessionSequence(1)));

        exception.ParamName.ShouldBe("endInclusive");
    }

    [Fact]
    public void CompactionSourceRange_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionSourceRange(new SessionSequence(1), new SessionSequence(2))
            .ShouldBe(new CompactionSourceRange(new SessionSequence(1), new SessionSequence(2)));

    [Fact]
    public void CompactionSourceSnapshot_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = Snapshot([Entry()]);
        var second = Snapshot([Entry()]);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void CompactionSourceSnapshot_Equality_WhenEntriesDiffer_InstancesAreNotEqual()
    {
        var first = Snapshot([Entry()]);
        var second = Snapshot([]);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void CompactionStrategyRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        StrategyRequest().ShouldBe(StrategyRequest());

    [Fact]
    public void CompactionCutSelectionRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionCutSelectionRequest(Request(), Snapshot()).ShouldBe(
            new CompactionCutSelectionRequest(Request(), Snapshot()));

    [Fact]
    public void CompactionValidationRequest_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionValidationRequest(Request(), Snapshot(), Cut(), Candidate()).ShouldBe(
            new CompactionValidationRequest(Request(), Snapshot(), Cut(), Candidate()));

    [Fact]
    public void CompactionTrigger_Equality_WhenSameValues_InstancesAreEqual() =>
        Trigger().ShouldBe(Trigger());

    [Fact]
    public void CompactionCheckpointProduced_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionCheckpointProduced(Checkpoint(), Producer(), new CompactionSizeEstimate(1, 1, 1)).ShouldBe(
            new CompactionCheckpointProduced(Checkpoint(), Producer(), new CompactionSizeEstimate(1, 1, 1)));

    [Fact]
    public void CompactionCutSelected_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionCutSelected(Cut()).ShouldBe(new CompactionCutSelected(Cut()));

    [Fact]
    public void NoSafeCompactionCut_Equality_WhenSameValues_InstancesAreEqual() =>
        new NoSafeCompactionCut(Rejection()).ShouldBe(new NoSafeCompactionCut(Rejection()));

    [Fact]
    public void CompactionCutSelectionFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionCutSelectionFailed(null!));

        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void CompactionCutSelectionFailed_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionCutSelectionFailed(Failure()).ShouldBe(new CompactionCutSelectionFailed(Failure()));

    [Fact]
    public void CompactionStrategyUnsupported_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionStrategyUnsupported(Rejection()).ShouldBe(new CompactionStrategyUnsupported(Rejection()));

    [Fact]
    public void CompactionStrategyFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionStrategyFailed(null!));

        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void CompactionStrategyFailed_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionStrategyFailed(Failure()).ShouldBe(new CompactionStrategyFailed(Failure()));

    [Fact]
    public void CompactionValidated_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionValidated(Candidate()).ShouldBe(new CompactionValidated(Candidate()));

    [Fact]
    public void CompactionValidationFailed_Constructor_WhenFailureNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionValidationFailed(null!));

        exception.ParamName.ShouldBe("failure");
    }

    [Fact]
    public void CompactionValidationFailed_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionValidationFailed(Failure()).ShouldBe(new CompactionValidationFailed(Failure()));

    [Fact]
    public void CompactionValidationIssue_Constructor_WhenSourceEntryIdsDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new CompactionValidationIssue(CompactionValidationIssueKind.InvalidStructure, "bad", default));

        exception.ParamName.ShouldBe("sourceEntryIds");
    }

    [Fact]
    public void CompactionValidationIssue_Equality_WhenSameValues_InstancesAreEqual() =>
        Issue().ShouldBe(Issue());

    [Fact]
    public void CompactionValidationIssue_Equality_WhenDifferentSourceEntryIds_InstancesAreNotEqual()
    {
        var id = new SessionEntryId(Guid.NewGuid());

        new CompactionValidationIssue(CompactionValidationIssueKind.InvalidStructure, "bad", [id]).ShouldNotBe(
            new CompactionValidationIssue(
                CompactionValidationIssueKind.InvalidStructure, "bad", [new SessionEntryId(Guid.NewGuid())]));
    }

    [Fact]
    public void CompactionValidationRejected_Constructor_WhenIssuesDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionValidationRejected(default));

        exception.ParamName.ShouldBe("issues");
    }

    [Fact]
    public void CompactionValidationRejected_Constructor_WhenIssuesEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new CompactionValidationRejected([]));

    [Fact]
    public void CompactionValidationRejected_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionValidationRejected([Issue()]).ShouldBe(new CompactionValidationRejected([Issue()]));

    [Fact]
    public void CompactionValidationRejected_Equality_WhenDifferentIssues_InstancesAreNotEqual()
    {
        var other = new CompactionValidationIssue(
            CompactionValidationIssueKind.NonReducing, "other", []);

        new CompactionValidationRejected([Issue()]).ShouldNotBe(new CompactionValidationRejected([other]));
    }

    [Fact]
    public void CompactionSucceeded_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionSucceeded(Context(), Record()).ShouldBe(new CompactionSucceeded(Context(), Record()));

    [Fact]
    public void CompactionConflict_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionConflict(Context(), new SessionVersion(1), new SessionVersion(2), Manifest()).ShouldBe(
            new CompactionConflict(Context(), new SessionVersion(1), new SessionVersion(2), Manifest()));

    [Fact]
    public void CompactionRejected_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionRejected(Context(), Rejection()).ShouldBe(new CompactionRejected(Context(), Rejection()));

    [Fact]
    public void CompactionNotReducing_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionNotReducing(Context(), new CompactionSizeEstimate(1, 1, 1), new CompactionSizeEstimate(1, 1, 1), 0.5).ShouldBe(
            new CompactionNotReducing(Context(), new CompactionSizeEstimate(1, 1, 1), new CompactionSizeEstimate(1, 1, 1), 0.5));

    [Fact]
    public void CompactionCancelled_Constructor_WhenSafeMessageInvalid_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionCancelled(Context(), "  "));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void CompactionCancelled_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionCancelled(Context(), "cancelled").ShouldBe(new CompactionCancelled(Context(), "cancelled"));

    [Fact]
    public void CompactionFailed_Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionFailed(Context(), Failure()).ShouldBe(new CompactionFailed(Context(), Failure()));

    [Fact]
    public void CompactionRecord_Constructor_WhenActiveWithoutCheckpoint_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionRecord(
            Context(),
            new SessionVersion(1),
            null,
            CompactionRecordStatus.Active,
            Manifest(),
            null,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void CompactionRecord_Constructor_WhenRejectedWithoutRejection_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionRecord(
            Context(),
            new SessionVersion(1),
            null,
            CompactionRecordStatus.Rejected,
            Manifest(),
            null,
            null,
            null,
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty));

        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void CompactionRecord_Equality_WhenSameValues_InstancesAreEqual() =>
        Record().ShouldBe(Record());

    [Fact]
    public void CompactionSessionEntry_Constructor_WhenRecordNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionSessionEntry(
            new SessionEntryId(Guid.NewGuid()),
            new SessionAddress(new AgentId(Guid.NewGuid()), new SessionId(Guid.NewGuid())),
            Correlation(),
            new BranchId(Guid.NewGuid()),
            new SessionSequence(1),
            null,
            DateTimeOffset.UnixEpoch,
            new SchemaVersion("1"),
            null!));

        exception.ParamName.ShouldBe("record");
    }

    private static readonly Guid _fixedOperationGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid _fixedRunGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private static InRunOperationCorrelation Correlation() =>
        new(new OperationId(_fixedOperationGuid), new RunId(_fixedRunGuid), null);

    private static ExecutionIdentity Identity() =>
        new(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human, ExtensionData.Empty);

    // Fixed GUIDs make the "same values" equality assertions above
    // deterministic without threading a shared instance through every
    // helper.
    private static readonly Guid _fixedCompactionGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _fixedAgentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _fixedSessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static CompactionOperationContext Context() => new(
        new CompactionId(_fixedCompactionGuid),
        new AgentId(_fixedAgentGuid),
        new SessionId(_fixedSessionGuid),
        Correlation(),
        Identity());

    private static CompactionTrigger Trigger() =>
        new(CompactionTriggerKind.ExplicitMaintenance, "test", null);

    private static CompactionRequest Request() => new(
        Context(),
        new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
        new SessionVersion(1),
        new SessionSequence(1),
        new ContextEpoch(0),
        Trigger(),
        100,
        0.5,
        1,
        DateTimeOffset.UnixEpoch,
        DateTimeOffset.UnixEpoch.AddMinutes(5),
        ExtensionData.Empty);

    private static CompactionSourceRange Range() => new(new SessionSequence(1), new SessionSequence(2));

    private static CompactionSourceSnapshot Snapshot() => Snapshot([]);

    private static CompactionSourceSnapshot Snapshot(ImmutableArray<SessionEntry> entries) => new(
        Context(), new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SessionVersion(1), new SessionSequence(1), entries);

    private static CompactionSessionEntry Entry() => new(
        new SessionEntryId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
        new SessionAddress(new AgentId(_fixedAgentGuid), new SessionId(_fixedSessionGuid)),
        Correlation(),
        new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
        new SessionSequence(1),
        null,
        DateTimeOffset.UnixEpoch,
        new SchemaVersion("1"),
        Record());

    private static CompactionCut Cut() => new(Range(), new SessionSequence(3), [new SessionEntryId(Guid.Parse("55555555-5555-5555-5555-555555555555"))]);

    private static CompactionCheckpoint Checkpoint() => new(
        [new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty);

    private static CompactionProducer Producer() =>
        new(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty);

    private static CompactionManifest Manifest() => new(
        new CompactionManifestId(Guid.Parse("66666666-6666-6666-6666-666666666666")),
        Context(),
        new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")),
        new SessionVersion(1),
        Range(),
        new SessionSequence(3),
        Producer(),
        new ContextEpoch(0),
        new CompactionSizeEstimate(10, 10, 1),
        new CompactionSizeEstimate(5, 5, 1),
        DateTimeOffset.UnixEpoch,
        ExtensionData.Empty);

    private static CompactionCandidate Candidate() => new(Manifest(), Checkpoint());

    private static CompactionStrategyRequest StrategyRequest() => new(Request(), Snapshot(), Cut());

    private static CompactionRejection Rejection() =>
        new(CompactionRejectionKind.NoSafeCut, "no safe cut", ExtensionData.Empty);

    private static CompactionFailure Failure() =>
        new(CompactionFailureKind.Unknown, "unknown", retryable: false, ExtensionData.Empty);

    private static CompactionValidationIssue Issue() => new(
        CompactionValidationIssueKind.InvalidStructure, "bad", [new SessionEntryId(Guid.Parse("77777777-7777-7777-7777-777777777777"))]);

    private static CompactionRecord Record() => new(
        Context(),
        new SessionVersion(1),
        new SessionVersion(2),
        CompactionRecordStatus.Active,
        Manifest(),
        Checkpoint(),
        null,
        null,
        DateTimeOffset.UnixEpoch,
        ExtensionData.Empty);
}
