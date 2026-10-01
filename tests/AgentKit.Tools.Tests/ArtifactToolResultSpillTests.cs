// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Tests;

using System.Text;

using AgentKit.TestSupport;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using static ToolRuntimeTestCalls;

/// <summary>Verifies <see cref="ArtifactToolResultSpill"/> stores complete results through its coordinator, falls back safely, and observes only content-free signals.</summary>
public sealed class ArtifactToolResultSpillTests
{
    private const string _secret = "sensitive-result-content";

    [Fact]
    public async Task SpillAsync_WhenTheCoordinatorAccepts_PreparesWithTheExactMetadataThenFinalizesAndReturnsTheReference()
    {
        var coordinator = new ScriptedCoordinator();
        var spill = Create(coordinator, new ToolResultSpillOptions
        {
            Directory = new ArtifactDirectoryId("tool-results"),
            RetentionPolicy = new ArtifactRetentionPolicyKey("short"),
            Classification = DataClassification.Confidential,
            MediaType = "text/markdown",
        });
        var call = Validated();
        var content = Encoding.UTF8.GetBytes(_secret);

        var result = await spill.SpillAsync(new ToolResultSpillRequest(call, [.. content]), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolResultSpilled>().Reference.ShouldBe(coordinator.Reference);
        var prepare = coordinator.Prepares.ShouldHaveSingleItem();
        prepare.AgentId.ShouldBe(AgentId);
        prepare.SessionId.ShouldBe(SessionId);
        prepare.ToolCallId.ShouldBe(call.CallId);
        prepare.DirectoryId.ShouldBe(new ArtifactDirectoryId("tool-results"));
        prepare.Authorization.ShouldBe(call.Authorization);
        prepare.Metadata.OwnerId.ShouldBe(new ArtifactOwnerId($"session:{SessionId}"));
        prepare.Metadata.MediaType.ShouldBe("text/markdown");
        prepare.Metadata.DeclaredLength.ShouldBe(content.Length);
        prepare.Metadata.DeclaredContentHash.ShouldBe(FileSecurityBinding.ContentFingerprint(content));
        prepare.Metadata.Classification.ShouldBe(DataClassification.Confidential);
        prepare.Metadata.Ownership.ShouldBe(ArtifactOwnershipKind.Session);
        prepare.Metadata.Mutability.ShouldBe(ArtifactMutability.Immutable);
        prepare.Metadata.Retention.Policy.ShouldBe(new ArtifactRetentionPolicyKey("short"));
        coordinator.PreparedBytes.ShouldBe(content);
        prepare.IdempotencyKey.Value.ShouldBe($"tool-result-spill:{call.CallId}:prepare");
        var finalize = coordinator.Finalizes.ShouldHaveSingleItem();
        finalize.PreparationId.ShouldBe(coordinator.PreparationId);
        finalize.IdempotencyKey.Value.ShouldBe($"tool-result-spill:{call.CallId}:finalize");
        coordinator.Aborts.ShouldBeEmpty();
    }

    [Fact]
    public async Task SpillAsync_WhenPreparationIsRejected_ReturnsNotSpilledWithoutFinalizing()
    {
        var coordinator = new ScriptedCoordinator { PrepareOutcome = new ArtifactPrepareRejected(new ArtifactFailure(ArtifactFailureKind.LimitExceeded, "too large")) };

        var result = await Create(coordinator).SpillAsync(Request(), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolResultNotSpilled>();
        coordinator.Finalizes.ShouldBeEmpty();
    }

    [Fact]
    public async Task SpillAsync_WhenFinalizationIsRejected_AbortsTheStagingAndReturnsNotSpilled()
    {
        var coordinator = new ScriptedCoordinator { FinalizeOutcome = new ArtifactFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.Denied, "denied")) };
        var call = Validated();

        var result = await Create(coordinator).SpillAsync(new ToolResultSpillRequest(call, [.. "abc"u8.ToArray()]), TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ToolResultNotSpilled>();
        var abort = coordinator.Aborts.ShouldHaveSingleItem();
        abort.PreparationId.ShouldBe(coordinator.PreparationId);
        abort.Reason.ShouldBe(ArtifactAbortReason.ReferenceCommitFailure);
        abort.IdempotencyKey.Value.ShouldBe($"tool-result-spill:{call.CallId}:abort");
    }

    [Fact]
    public async Task SpillAsync_WhenTheCoordinatorFaults_ReturnsNotSpilledWithoutLeakingTheException()
    {
        var coordinator = new ScriptedCoordinator { PrepareFault = new InvalidOperationException(_secret) };

        var result = await Create(coordinator).SpillAsync(Request(), TestContext.Current.CancellationToken);

        result.ShouldBeOfType<ToolResultNotSpilled>().SafeReason.ShouldNotContain(_secret);
    }

    [Fact]
    public async Task SpillAsync_WhenTheSequenceExceedsItsTimeout_ReturnsNotSpilled()
    {
        var time = new FakeTimeProvider();
        var coordinator = new ScriptedCoordinator { PrepareGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        var spill = Create(coordinator, time: time, timeout: TimeSpan.FromSeconds(10));

        var pending = spill.SpillAsync(Request(), TestContext.Current.CancellationToken).AsTask();
        time.Advance(TimeSpan.FromSeconds(10));
        var result = await pending;

        _ = result.ShouldBeOfType<ToolResultNotSpilled>();
    }

    [Fact]
    public async Task SpillAsync_WhenTheCallerCancels_PropagatesCancellation()
    {
        var coordinator = new ScriptedCoordinator { PrepareGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var source = new CancellationTokenSource();
        var pending = Create(coordinator).SpillAsync(Request(), source.Token).AsTask();
        await source.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await pending);
    }

    [Fact]
    public async Task SpillAsync_WhenObserved_EmitsOneActivityLogAndMetricThatCarryNoContent()
    {
        var logger = new RecordingLogger<ArtifactToolResultSpill>();
        var call = Validated();
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ToolResultSpill
                && observation.GetTagItem(AgentKitTagNames.ToolCallId)?.ToString() == call.CallId.ToString());
        using var metrics = new MetricCollector(AgentKitMetricNames.ToolResultSpillCount);
        var spill = new ArtifactToolResultSpill(new ScriptedCoordinator(), Options(), TimeProvider.System, logger);

        _ = await spill.SpillAsync(new ToolResultSpillRequest(call, [.. Encoding.UTF8.GetBytes(_secret)]), TestContext.Current.CancellationToken);

        _ = activities.Snapshot().ShouldHaveSingleItem();
        logger.Snapshot().ShouldContain(static entry => entry.EventId.Id == 4141 && entry.Level == Microsoft.Extensions.Logging.LogLevel.Information);
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), metrics.Snapshot(), _secret);
    }

    [Fact]
    public async Task SpillAsync_WhenTheRequestIsNull_ThrowsNamingItBeforeAnyEffect()
    {
        var coordinator = new ScriptedCoordinator();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await Create(coordinator).SpillAsync(null!))).ParamName.ShouldBe("request");
        coordinator.Prepares.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenADependencyOrOptionIsInvalid_ThrowsNamingIt()
    {
        var coordinator = new ScriptedCoordinator();
        var logger = NullLogger<ArtifactToolResultSpill>.Instance;

        Should.Throw<ArgumentNullException>(() => new ArtifactToolResultSpill(null!, Options(), TimeProvider.System, logger)).ParamName.ShouldBe("artifacts");
        Should.Throw<ArgumentNullException>(() => new ArtifactToolResultSpill(coordinator, null!, TimeProvider.System, logger)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentNullException>(() => new ArtifactToolResultSpill(coordinator, Options(), null!, logger)).ParamName.ShouldBe("time");
        Should.Throw<ArgumentNullException>(() => new ArtifactToolResultSpill(coordinator, Options(), TimeProvider.System, null!)).ParamName.ShouldBe("logger");
        Should.Throw<ArgumentException>(() => new ArtifactToolResultSpill(coordinator, new ToolResultSpillOptions(), TimeProvider.System, logger)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentException>(() => new ArtifactToolResultSpill(coordinator, new ToolResultSpillOptions { Directory = new ArtifactDirectoryId("d"), MediaType = " " }, TimeProvider.System, logger)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactToolResultSpill(coordinator, new ToolResultSpillOptions { Directory = new ArtifactDirectoryId("d"), Timeout = TimeSpan.Zero }, TimeProvider.System, logger)).ParamName.ShouldBe("options");
        Should.Throw<ArgumentOutOfRangeException>(() => new ArtifactToolResultSpill(coordinator, new ToolResultSpillOptions { Directory = new ArtifactDirectoryId("d"), Classification = (DataClassification) 99 }, TimeProvider.System, logger)).ParamName.ShouldBe("options");
    }

    private static ToolResultSpillOptions Options(TimeSpan? timeout = null) =>
        new() { Directory = new ArtifactDirectoryId("tool-results"), Timeout = timeout ?? TimeSpan.FromSeconds(30) };

    private static ArtifactToolResultSpill Create(
        ScriptedCoordinator coordinator, ToolResultSpillOptions? options = null, FakeTimeProvider? time = null, TimeSpan? timeout = null) =>
        new(coordinator, options ?? Options(timeout), time ?? new FakeTimeProvider(), NullLogger<ArtifactToolResultSpill>.Instance);

    private static ToolResultSpillRequest Request() => new(Validated(), [.. "payload"u8.ToArray()]);

    private sealed class ScriptedCoordinator: IArtifactCoordinator
    {
        public ArtifactPreparationId PreparationId { get; } = new(Guid.Parse("b0000000-0000-0000-0000-0000000000b1"));

        public ArtifactReference Reference { get; } = new(
            new ArtifactId(Guid.Parse("a0000000-0000-0000-0000-0000000000a1")), new ArtifactVersion("1"), new ArtifactDirectoryId("tool-results"),
            new ArtifactProfileKey("artifacts"), new ArtifactProfileVersion(1), new TenantId("tenant"), new ArtifactOwnerId("session:test"),
            new PrincipalId("principal"), "text/plain", 7, new ArtifactIntegrity(FileSecurityBinding.ContentFingerprint("x"u8), DateTimeOffset.UnixEpoch),
            DataClassification.Internal, ArtifactOwnershipKind.Session, ArtifactMutability.Immutable,
            new ArtifactRetention(new ArtifactRetentionPolicyKey("session"), null, false), null, DateTimeOffset.UnixEpoch);

        public List<ArtifactPrepareRequest> Prepares { get; } = [];
        public List<ArtifactFinalizeRequest> Finalizes { get; } = [];
        public List<ArtifactAbortRequest> Aborts { get; } = [];
        public byte[] PreparedBytes { get; private set; } = [];
        public ArtifactPrepareResult? PrepareOutcome { get; set; }
        public ArtifactFinalizeResult? FinalizeOutcome { get; set; }
        public Exception? PrepareFault { get; set; }
        public TaskCompletionSource? PrepareGate { get; set; }

        public async Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default)
        {
            Prepares.Add(request);
            using var buffer = new MemoryStream();
            await request.Content.CopyToAsync(buffer, cancellationToken);
            PreparedBytes = buffer.ToArray();
            if (PrepareGate is { } gate)
            {
                await gate.Task.WaitAsync(cancellationToken);
            }

            return PrepareFault is { } fault
                ? throw fault
                : PrepareOutcome ?? new ArtifactPrepared(PreparationId, Reference.Id, Reference.Version, DateTimeOffset.UnixEpoch.AddHours(1));
        }

        public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default)
        {
            Finalizes.Add(request);
            return ValueTask.FromResult(FinalizeOutcome ?? new ArtifactFinalized(Reference));
        }

        public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default)
        {
            Aborts.Add(request);
            return ValueTask.FromResult<ArtifactAbortResult>(new ArtifactAborted(AlreadyAbsent: false));
        }

        public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public ValueTask<ArtifactReconciliationResult> ReconcileAsync(ArtifactReconciliationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
