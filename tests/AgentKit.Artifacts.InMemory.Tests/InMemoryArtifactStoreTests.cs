// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.InMemory.Tests;

using System.Diagnostics;

using AgentKit.Observability;
using AgentKit.TestSupport;

using Microsoft.Extensions.Logging;

/// <summary>Verifies <see cref="InMemoryArtifactStore"/> argument constraints, grant enforcement, and observability in addition to the shared contract suite.</summary>
public sealed class InMemoryArtifactStoreTests: ArtifactStoreConformanceTests<InMemoryArtifactStoreConformanceFixture>
{
    private static readonly DateTimeOffset _now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <inheritdoc/>
    protected override InMemoryArtifactStoreConformanceFixture CreateFixture() => new();

    [Fact]
    public void Constructor_WhenADependencyIsNull_ThrowsWithExactParameterName()
    {
        var grants = new IntentReceiptGrantStore();
        var clock = new FakeTimeProvider(_now);
        var ids = new GuidEnforcementIntentIdGenerator();

        Should.Throw<ArgumentNullException>(() => new InMemoryArtifactStore(null!, clock)).ParamName.ShouldBe("grants");
        Should.Throw<ArgumentNullException>(() => new InMemoryArtifactStore(grants, null!)).ParamName.ShouldBe("time");
        Should.Throw<ArgumentNullException>(() => new InMemoryArtifactStore(grants, clock, null!)).ParamName.ShouldBe("intentIds");
        Should.Throw<ArgumentNullException>(() => new InMemoryArtifactStore(null!, clock, ids)).ParamName.ShouldBe("grants");
    }

    [Fact]
    public void SecurityAudience_WhenConstructed_NamesTheInMemoryBackend()
    {
        var store = new InMemoryArtifactStore(new IntentReceiptGrantStore(), new FakeTimeProvider(_now));

        store.SecurityAudience.ShouldBe(new ComponentId("agentkit.artifacts.in-memory"));
    }

    [Fact]
    public async Task Operations_WhenRequestIsNull_ThrowArgumentNullExceptionBeforeAnyEffect()
    {
        var store = new InMemoryArtifactStore(new IntentReceiptGrantStore(), new FakeTimeProvider(_now));

        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.PrepareAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.FinalizeAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.AbortAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.ReadAsync(null!))).ParamName.ShouldBe("request");
        (await Should.ThrowAsync<ArgumentNullException>(async () => await store.DeleteAsync(null!))).ParamName.ShouldBe("request");
    }

    [Theory]
    [InlineData(nameof(GrantConsumptionStatus.Reconciled))]
    [InlineData(nameof(GrantConsumptionStatus.Revoked))]
    public async Task PrepareAsync_WhenTheGrantStoreDoesNotConsume_DoesNotCreateState(string status)
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grants = new IntentReceiptGrantStore { Status = Enum.Parse<GrantConsumptionStatus>(status) };
        var store = new InMemoryArtifactStore(grants, new FakeTimeProvider(_now));
        var request = fixture.CreatePrepare("content"u8.ToArray());

        var rejected = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        grants.Status = GrantConsumptionStatus.Consumed;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactStorePrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenTheConsumptionReceiptIsNotFreshAndExact_DoesNotCreateState()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grants = new IntentReceiptGrantStore { ReturnExactReceipt = false };
        var store = new InMemoryArtifactStore(grants, new FakeTimeProvider(_now));
        var request = fixture.CreatePrepare("content"u8.ToArray());

        var rejected = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        grants.ReturnExactReceipt = true;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.SafeMessage.ShouldContain("enforcement-intent receipt");
        _ = accepted.ShouldBeOfType<ArtifactStorePrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenTheGrantStoreIsUnavailable_DeniesBeforeAnyState()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grants = new IntentReceiptGrantStore { Throw = true };
        var store = new InMemoryArtifactStore(grants, new FakeTimeProvider(_now));
        var request = fixture.CreatePrepare("content"u8.ToArray());

        var rejected = await store.PrepareAsync(request, TestContext.Current.CancellationToken);
        grants.Throw = false;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
        _ = accepted.ShouldBeOfType<ArtifactStorePrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenCallerCancelsDuringNonCooperativeConsumption_DoesNotCreateState()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        using var cancellation = new CancellationTokenSource();
        var grants = new IntentReceiptGrantStore { OnConsumption = cancellation.Cancel };
        var store = new InMemoryArtifactStore(grants, new FakeTimeProvider(_now));
        var request = fixture.CreatePrepare("content"u8.ToArray());

        var action = async () => await store.PrepareAsync(request, cancellation.Token);
        _ = await action.ShouldThrowAsync<OperationCanceledException>();
        grants.OnConsumption = null;
        var accepted = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        _ = accepted.ShouldBeOfType<ArtifactStorePrepared>();
    }

    [Fact]
    public async Task PrepareAsync_WhenConsumed_BindsTheExactOperationEvidence()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var grants = new IntentReceiptGrantStore();
        var store = new InMemoryArtifactStore(grants, new FakeTimeProvider(_now));
        var request = fixture.CreatePrepare("content"u8.ToArray());

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        var enforcement = grants.LastEnforcement.ShouldNotBeNull();
        enforcement.Audience.ShouldBe(store.SecurityAudience);
        enforcement.Kind.ShouldBe(SecurityOperationKind.Artifact);
        enforcement.Effect.ShouldBe(SecurityEffect.Create);
        enforcement.Scope.ShouldBe(request.Scope);
        enforcement.Identity.ShouldBe(request.Identity);
        enforcement.Authorization.ShouldBe(request.Grant.Authorization);
        enforcement.Resources.ShouldBe(
            [ArtifactSecurityBinding.ArtifactResource(request.ArtifactId), ArtifactSecurityBinding.PreparationResource(request.PreparationId)]);
    }

    [Theory]
    [InlineData("version")]
    [InlineData("profile-key")]
    [InlineData("profile-version")]
    [InlineData("created-at")]
    [InlineData("expires-at")]
    [InlineData("content-hash")]
    public async Task PrepareAsync_WhenBoundAttemptFieldChanges_CannotUseExistingGrant(string field)
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var authorized = fixture.CreatePrepare("content"u8.ToArray());
        var changed = new ArtifactStorePrepareRequest(
            authorized.ArtifactId, authorized.PreparationId,
            field == "version" ? new ArtifactVersion("2") : authorized.Version,
            field == "profile-key" ? new ArtifactProfileKey("other") : authorized.ProfileKey,
            field == "profile-version" ? new ArtifactProfileVersion(2) : authorized.ProfileVersion,
            authorized.TenantId, authorized.CreatedBy, authorized.DirectoryId, authorized.Metadata, authorized.Content,
            field == "content-hash" ? FileSecurityBinding.ContentFingerprint("other"u8) : authorized.ContentHash,
            field == "created-at" ? authorized.CreatedAt.AddSeconds(1) : authorized.CreatedAt,
            field == "expires-at" ? authorized.ExpiresAt.AddSeconds(1) : authorized.ExpiresAt,
            authorized.Grant, authorized.IdempotencyKey);
        await fixture.RegisterGrantAsync(authorized.Grant, TestContext.Current.CancellationToken);

        var rejected = await store.PrepareAsync(changed, TestContext.Current.CancellationToken);

        rejected.ShouldBeOfType<ArtifactStorePrepareRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.Denied);
    }

    [Fact]
    public async Task PrepareAsync_WhenObserved_EmitsOneSpanWithTenantAndBoundedMetric()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = fixture.CreatePrepare("protected artifact body"u8.ToArray());
        await fixture.RegisterGrantAsync(request.Grant, TestContext.Current.CancellationToken);
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == request.TenantId.Value);
        using var metrics = new MetricCollector(AgentKitMetricNames.ArtifactStoreOperationCount);
        using var durations = new MetricCollector(AgentKitMetricNames.ArtifactStoreOperationDuration);

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Ok);
        span.GetTagItem(AgentKitTagNames.ArtifactStoreOperation).ShouldBe("prepare");
        span.GetTagItem(AgentKitTagNames.ArtifactStoreAdapter).ShouldBe("in_memory");
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("completed");
        metrics.Snapshot().ShouldContain(static measurement =>
            measurement.Tags[AgentKitTagNames.ArtifactStoreOperation]!.Equals("prepare") && measurement.Tags[AgentKitTagNames.Outcome]!.Equals("completed"));
        metrics.Snapshot().ShouldAllBe(static measurement => measurement.Tags.Keys.Order().SequenceEqual(
            new[] { AgentKitTagNames.ArtifactStoreAdapter, AgentKitTagNames.ArtifactStoreOperation, AgentKitTagNames.Outcome }.Order()));
        durations.Snapshot().ShouldNotBeEmpty();
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), [], metrics.Snapshot(), "protected artifact body", "conformance:owner");
    }

    [Fact]
    public async Task PrepareAsync_WhenDenied_MarksTheSpanFailedAndLogsContentFree()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        _ = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var logger = new RecordingLogger<InMemoryArtifactStore>();
        var store = new InMemoryArtifactStore(new IntentReceiptGrantStore { Status = GrantConsumptionStatus.Revoked }, new FakeTimeProvider(_now), new GuidEnforcementIntentIdGenerator(), logger);
        var request = fixture.CreatePrepare("a confidential artifact"u8.ToArray(), idempotencyKey: "secret-replay-key");
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == request.TenantId.Value);

        _ = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        var span = activities.Snapshot().ShouldHaveSingleItem();
        span.Status.ShouldBe(ActivityStatusCode.Error);
        span.GetTagItem(AgentKitTagNames.Outcome).ShouldBe("denied");
        var entry = logger.Snapshot().ShouldHaveSingleItem();
        entry.EventId.Id.ShouldBe(29100);
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Message.ShouldNotContain("confidential");
        SignalAssertions.ShouldNotContainContent(activities.Snapshot(), logger.Snapshot(), [], "a confidential artifact", request.IdempotencyKey.Value);
    }

    [Fact]
    public async Task PrepareAsync_WhenTheLoggerThrows_StillReturnsTheSemanticResult()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        var authority = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var store = new InMemoryArtifactStore(new IntentReceiptGrantStore(), new FakeTimeProvider(_now), new GuidEnforcementIntentIdGenerator(), new RecordingLogger<InMemoryArtifactStore> { ThrowOnWrite = true });
        var request = fixture.CreatePrepare("content"u8.ToArray());

        var result = await store.PrepareAsync(request, TestContext.Current.CancellationToken);

        _ = result.ShouldBeOfType<ArtifactStorePrepared>();
        authority.SecurityAudience.ShouldBe(store.SecurityAudience);
    }

    [Fact]
    public async Task PrepareAsync_WhenCancelled_RecordsACancelledOutcomeAndRethrows()
    {
        await using var fixture = new InMemoryArtifactStoreConformanceFixture();
        var store = await fixture.CreateAsync(TestContext.Current.CancellationToken);
        var request = fixture.CreatePrepare("content"u8.ToArray());
        using var activities = new ActivityCollector(
            static source => source.Name == AgentKitDiagnostics.ActivitySourceName,
            observation => observation.OperationName == AgentKitActivityNames.ArtifactStoreOperation
                && observation.GetTagItem(AgentKitTagNames.TenantId)?.ToString() == request.TenantId.Value);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await store.PrepareAsync(request, cancelled.Token));

        activities.Snapshot().ShouldHaveSingleItem().GetTagItem(AgentKitTagNames.Outcome).ShouldBe("cancelled");
    }
}
