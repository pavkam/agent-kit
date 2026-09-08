// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Continuation;

using AgentKit.TestSupport;

public sealed class CompactionRetryContinuationCauseTests
{
    private static readonly AgentId _agentId = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly SessionId _sessionId = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly ExecutionLaneId _laneId = new("main");
    private static readonly OperationId _installedOperationId = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly OperationId _compactionOperationId = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static readonly RunId _runId = new(Guid.Parse("55555555-5555-5555-5555-555555555555"));
    private static readonly TurnId _turnId = new(Guid.Parse("66666666-6666-6666-6666-666666666666"));
    private static readonly ModelRequestId _requestId = new(Guid.Parse("77777777-7777-7777-7777-777777777777"));
    private static readonly BranchId _branchId = new(Guid.Parse("88888888-8888-8888-8888-888888888888"));

    [Fact]
    public void Constructor_WhenModelRequestIdIsDefault_ThrowsExactArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new CompactionRetryContinuationCause(default, Compaction()));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("modelRequestId");
    }

    [Fact]
    public void Constructor_WhenCompactionIsNull_ThrowsExactArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new CompactionRetryContinuationCause(_requestId, null!));

        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("compaction");
    }

    [Fact]
    public void Constructor_WhenRetainedRecordIsNull_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(() => new CompactionRetryContinuationCause(
            _requestId, Compaction() with { Record = null! }));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("compaction");
    }

    [Theory]
    [InlineData("resultContext")]
    [InlineData("recordContext")]
    [InlineData("manifest")]
    [InlineData("manifestContext")]
    [InlineData("checkpoint")]
    [InlineData("activatedVersion")]
    [InlineData("correlation")]
    [InlineData("identity")]
    public void Constructor_WhenRetainedEvidenceIsMalformed_ThrowsExactArgumentException(string part)
    {
        var malformed = WithMalformedPart(Compaction(), part);

        var exception = Should.Throw<ArgumentException>(() => new CompactionRetryContinuationCause(_requestId, malformed));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("compaction");
    }

    [Fact]
    public void Constructor_WhenResultAndRecordContextsAreSwapped_ThrowsExactArgumentException()
    {
        var compaction = Compaction();
        var swapped = compaction with { Record = compaction.Record with { Context = OtherContext() } };

        var exception = Should.Throw<ArgumentException>(() => new CompactionRetryContinuationCause(_requestId, swapped));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("compaction");
    }

    [Fact]
    public void Constructor_WhenRecordAndManifestContextsAreSwapped_ThrowsExactArgumentException()
    {
        var compaction = Compaction();
        var swapped = compaction with
        {
            Record = compaction.Record with
            {
                Manifest = compaction.Record.Manifest with { Context = OtherContext() },
            },
        };

        var exception = Should.Throw<ArgumentException>(() => new CompactionRetryContinuationCause(_requestId, swapped));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("compaction");
    }

    [Theory]
    [InlineData("compactionId")]
    [InlineData("agentId")]
    [InlineData("sessionId")]
    [InlineData("manifestId")]
    [InlineData("branchId")]
    public void Constructor_WhenRetainedIdentityIsDefault_ThrowsExactArgumentException(string identity)
    {
        var malformed = WithDefaultIdentity(Compaction(), identity);

        var exception = Should.Throw<ArgumentException>(() => new CompactionRetryContinuationCause(_requestId, malformed));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("compaction");
    }

    [Fact]
    public void RunContinuationContext_WhenManifestBelongsToForeignBranch_ThrowsExactArgumentException()
    {
        var compaction = Compaction();
        var foreign = compaction with
        {
            Record = compaction.Record with
            {
                Manifest = compaction.Record.Manifest with
                {
                    BranchId = new BranchId(Guid.Parse("99999999-9999-9999-9999-999999999999")),
                },
            },
        };
        var cause = new CompactionRetryContinuationCause(_requestId, foreign);

        var exception = Should.Throw<ArgumentException>(() => Context(cause));

        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("causes");
    }

    [Fact]
    public void RunContinuationContext_WhenCompactionUsesDistinctSuboperation_AcceptsCausalEvidence()
    {
        var cause = new CompactionRetryContinuationCause(_requestId, Compaction());

        var context = Context(cause);

        context.OperationId.ShouldBe(_installedOperationId);
        cause.Compaction.Context.Correlation.OperationId.ShouldBe(_compactionOperationId);
        cause.Compaction.Context.Correlation.OperationId.ShouldNotBe(context.OperationId);
    }

    private static RunContinuationContext Context(CompactionRetryContinuationCause cause) =>
        new(
            _agentId,
            _sessionId,
            _laneId,
            _installedOperationId,
            _runId,
            AgentRunState.WaitingRetry,
            new OperationStateRevision(4),
            new SessionBranchCursor(_branchId, new SessionEntryId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))),
            new SessionSequence(12),
            new ConfigurationVersion(2),
            new RunPolicyVersion(1),
            new RetryContinuationBoundary(_turnId, _requestId),
            requiredStopOutcome: null,
            [cause]);

    private static CompactionSucceeded WithDefaultIdentity(CompactionSucceeded compaction, string identity)
    {
        var context = compaction.Context with
        {
            CompactionId = identity == "compactionId" ? default : compaction.Context.CompactionId,
            AgentId = identity == "agentId" ? default : compaction.Context.AgentId,
            SessionId = identity == "sessionId" ? default : compaction.Context.SessionId,
        };
        var manifest = compaction.Record.Manifest with
        {
            Id = identity == "manifestId" ? default : compaction.Record.Manifest.Id,
            Context = context,
            BranchId = identity == "branchId" ? default : compaction.Record.Manifest.BranchId,
        };
        return compaction with
        {
            Context = context,
            Record = compaction.Record with { Context = context, Manifest = manifest },
        };
    }

    private static CompactionSucceeded WithMalformedPart(CompactionSucceeded compaction, string part)
    {
        if (part == "resultContext")
        {
            return compaction with { Context = null! };
        }
        if (part == "recordContext")
        {
            return compaction with { Record = compaction.Record with { Context = null! } };
        }
        if (part == "manifest")
        {
            return compaction with { Record = compaction.Record with { Manifest = null! } };
        }
        if (part == "manifestContext")
        {
            return compaction with
            {
                Record = compaction.Record with
                {
                    Manifest = compaction.Record.Manifest with { Context = null! },
                },
            };
        }
        if (part == "checkpoint")
        {
            return compaction with { Record = compaction.Record with { Checkpoint = null } };
        }
        if (part == "activatedVersion")
        {
            return compaction with { Record = compaction.Record with { ActivatedSessionVersion = null } };
        }

        var context = part == "correlation"
            ? compaction.Context with { Correlation = null! }
            : compaction.Context with { Identity = null! };
        return compaction with
        {
            Context = context,
            Record = compaction.Record with
            {
                Context = context,
                Manifest = compaction.Record.Manifest with { Context = context },
            },
        };
    }

    private static CompactionSucceeded Compaction()
    {
        var context = ContextValue();
        var manifest = new CompactionManifest(
            new CompactionManifestId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            context,
            _branchId,
            new SessionVersion(3),
            new CompactionSourceRange(new SessionSequence(1), new SessionSequence(5)),
            new SessionSequence(6),
            new CompactionProducer(new CompactionStrategyKey("test"), deterministic: true, ExtensionData.Empty),
            new ContextEpoch(1),
            new CompactionSizeEstimate(20, 100, 1),
            new CompactionSizeEstimate(5, 25, 1),
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        var checkpoint = new CompactionCheckpoint(
            [new TextPart("summary", TextSemantics.Plain, ExtensionData.Empty)],
            ExtensionData.Empty);
        var record = new CompactionRecord(
            context,
            new SessionVersion(3),
            new SessionVersion(4),
            CompactionRecordStatus.Active,
            manifest,
            checkpoint,
            supersedes: null,
            rejection: null,
            DateTimeOffset.UnixEpoch,
            ExtensionData.Empty);
        return new CompactionSucceeded(context, record);
    }

    private static CompactionOperationContext ContextValue() =>
        new(
            new CompactionId(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")),
            _agentId,
            _sessionId,
            new InRunOperationCorrelation(_compactionOperationId, _runId, _turnId),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));

    private static CompactionOperationContext OtherContext() =>
        new(
            new CompactionId(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd")),
            _agentId,
            _sessionId,
            new InRunOperationCorrelation(_compactionOperationId, _runId, _turnId),
            TestExecutionIdentity.Create(new TenantId("tenant"), new PrincipalId("principal"), ExecutionSubjectKind.Human));
}
