// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Compaction;

using AgentKit;

/// <summary>Verifies CompactionCancelled behavior and contracts.</summary>
public sealed class CompactionCancelledTests
{
    [Fact]
    public void Constructor_WhenSafeMessageInvalid_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionCancelled(Context(), CompactionCommitState.NotAttempted, "  "));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenContextNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionCancelled(null!, CompactionCommitState.NotAttempted, "cancelled"));
        exception.ParamName.ShouldBe("context");
    }

    [Fact]
    public void Constructor_WhenCommitStateUndefined_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new CompactionCancelled(Context(), (CompactionCommitState) 99, "cancelled"));
        exception.ParamName.ShouldBe("commitState");
    }

    [Fact]
    public void Constructor_WhenCommittedWithoutRecord_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionCancelled(Context(), CompactionCommitState.Committed, "cancelled"));
        exception.ParamName.ShouldBe("committedRecord");
    }

    [Theory]
    [InlineData(CompactionCommitState.NotAttempted)]
    [InlineData(CompactionCommitState.NotCommitted)]
    [InlineData(CompactionCommitState.Unknown)]
    public void Constructor_WhenNotCommittedButRecordSupplied_ThrowsArgumentException(CompactionCommitState state)
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionCancelled(Context(), state, "cancelled", Record()));
        exception.ParamName.ShouldBe("committedRecord");
    }

    [Fact]
    public void Constructor_WhenCommittedWithRecord_ExposesRecord()
    {
        var record = Record();

        var cancelled = new CompactionCancelled(Context(), CompactionCommitState.Committed, "cancelled after commit", record);

        cancelled.CommitState.ShouldBe(CompactionCommitState.Committed);
        cancelled.CommittedRecord.ShouldBe(record);
    }

    [Theory]
    [InlineData(CompactionCommitState.NotAttempted)]
    [InlineData(CompactionCommitState.NotCommitted)]
    [InlineData(CompactionCommitState.Unknown)]
    public void Constructor_WhenNotCommitted_LeavesRecordNull(CompactionCommitState state)
    {
        var cancelled = new CompactionCancelled(Context(), state, "cancelled");

        cancelled.CommitState.ShouldBe(state);
        cancelled.CommittedRecord.ShouldBeNull();
    }

    [Fact]
    public void Equality_WhenSameValues_InstancesAreEqual() =>
        new CompactionCancelled(Context(), CompactionCommitState.NotAttempted, "cancelled")
            .ShouldBe(new CompactionCancelled(Context(), CompactionCommitState.NotAttempted, "cancelled"));

    private static readonly Guid _fixedOperationGuid = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid _fixedRunGuid = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static InRunOperationCorrelation Correlation() => new(new OperationId(_fixedOperationGuid), new RunId(_fixedRunGuid), null);
    private static ExecutionIdentity Identity() => TestSupport.TestExecutionIdentity.Create(new TenantId("t"), new PrincipalId("p"), ExecutionSubjectKind.Human);
    // Fixed GUIDs make the "same values" equality assertions above
    // deterministic without threading a shared instance through every
    // helper.
    private static readonly Guid _fixedCompactionGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid _fixedAgentGuid = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _fixedSessionGuid = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static CompactionOperationContext Context() => TestSupport.TestSecurityEvidence.CompactionContext(new CompactionId(_fixedCompactionGuid), new AgentId(_fixedAgentGuid), new SessionId(_fixedSessionGuid), Correlation(), Identity());
    private static CompactionManifest Manifest() => new(new CompactionManifestId(Guid.Parse("66666666-6666-6666-6666-666666666666")), Context(), new BranchId(Guid.Parse("44444444-4444-4444-4444-444444444444")), new SessionVersion(1), new CompactionSourceRange(new SessionSequence(1), new SessionSequence(2)), new SessionSequence(3), new CompactionProducer(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty), new ContextEpoch(0), new CompactionSizeEstimate(10, 10, 1), new CompactionSizeEstimate(5, 5, 1), DateTimeOffset.UnixEpoch, ExtensionData.Empty);
    private static CompactionRecord Record() => new(Context(), new SessionVersion(1), new SessionVersion(2), CompactionRecordStatus.Active, Manifest(), new CompactionCheckpoint([new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)], ExtensionData.Empty), supersedes: null, rejection: null, DateTimeOffset.UnixEpoch, ExtensionData.Empty);
}
