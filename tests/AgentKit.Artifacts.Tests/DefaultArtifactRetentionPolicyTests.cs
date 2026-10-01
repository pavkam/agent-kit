// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

/// <summary>Verifies <see cref="DefaultArtifactRetentionPolicy"/> resolution and deletion decisions.</summary>
public sealed class DefaultArtifactRetentionPolicyTests
{
    private static readonly DateTimeOffset _now = ArtifactTestData.Now;
    private static readonly ArtifactRetentionPolicyKey _session = new("session");

    [Fact]
    public async Task ResolveAsync_WhenTheRequestNamesItsOwnRetention_KeepsItAndInheritsOnlyTheStricterHold()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        var request = new ArtifactRetentionRequest(
            Metadata(new ArtifactRetention(_session, _now.AddDays(1), false)), new ArtifactRetention(_session, _now.AddDays(30), true), _now);

        var decision = await policy.ResolveAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<ArtifactRetentionAllowed>().Retention.ShouldBe(new ArtifactRetention(_session, _now.AddDays(1), true));
    }

    [Fact]
    public async Task ResolveAsync_WhenTheRequestOmitsExpiryAndSharesThePolicy_InheritsTheProfileExpiry()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        var request = new ArtifactRetentionRequest(
            Metadata(new ArtifactRetention(_session, null, false)), new ArtifactRetention(_session, _now.AddDays(30), false), _now);

        var decision = await policy.ResolveAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<ArtifactRetentionAllowed>().Retention.ShouldBe(new ArtifactRetention(_session, _now.AddDays(30), false));
    }

    [Fact]
    public async Task ResolveAsync_WhenTheRequestNamesADifferentPolicy_DoesNotInheritTheProfileExpiry()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        var request = new ArtifactRetentionRequest(
            Metadata(new ArtifactRetention(new ArtifactRetentionPolicyKey("evidence"), null, false)), new ArtifactRetention(_session, _now.AddDays(30), false), _now);

        var decision = await policy.ResolveAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<ArtifactRetentionAllowed>().Retention.ExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_WhenTheRequestedExpiryAlreadyPassed_RejectsWithARetentionConflict()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        var request = new ArtifactRetentionRequest(
            Metadata(new ArtifactRetention(_session, _now, false)), new ArtifactRetention(_session, null, false), _now);

        var decision = await policy.ResolveAsync(request, TestContext.Current.CancellationToken);

        decision.ShouldBeOfType<ArtifactRetentionRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
    }

    [Fact]
    public async Task ResolveAsync_WhenCancelledOrNull_Throws()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var request = new ArtifactRetentionRequest(Metadata(new ArtifactRetention(_session, null, false)), new ArtifactRetention(_session, null, false), _now);

        (await Should.ThrowAsync<ArgumentNullException>(async () => await policy.ResolveAsync(null!))).ParamName.ShouldBe("request");
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.ResolveAsync(request, cancelled.Token));
    }

    [Fact]
    public async Task EvaluateDeletionAsync_WhenHeldOrExternallyGoverned_RejectsAndOtherwiseAllows()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        var open = Reference(new ArtifactRetention(_session, null, false));
        var held = Reference(new ArtifactRetention(_session, null, true));
        var keeps = Reference(new ArtifactRetention(_session, null, false), external: new ExternalArtifactOwnership(new ExternalArtifactResourceId("v:1"), new Uri("https://files.example.com/1"), false));
        var delegated = Reference(new ArtifactRetention(_session, null, false), external: new ExternalArtifactOwnership(new ExternalArtifactResourceId("v:2"), new Uri("https://files.example.com/2"), true));

        (await policy.EvaluateDeletionAsync(open, _now, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactRetentionAllowed>().Retention.ShouldBe(open.Retention);
        (await policy.EvaluateDeletionAsync(held, _now, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactRetentionRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        (await policy.EvaluateDeletionAsync(keeps, _now, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactRetentionRejected>().Failure.Kind.ShouldBe(ArtifactFailureKind.RetentionConflict);
        _ = (await policy.EvaluateDeletionAsync(delegated, _now, TestContext.Current.CancellationToken)).ShouldBeOfType<ArtifactRetentionAllowed>();
    }

    [Fact]
    public async Task EvaluateDeletionAsync_WhenCancelledOrNull_Throws()
    {
        var policy = new DefaultArtifactRetentionPolicy();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        (await Should.ThrowAsync<ArgumentNullException>(async () => await policy.EvaluateDeletionAsync(null!, _now))).ParamName.ShouldBe("reference");
        _ = await Should.ThrowAsync<OperationCanceledException>(async () => await policy.EvaluateDeletionAsync(Reference(new ArtifactRetention(_session, null, false)), _now, cancelled.Token));
    }

    private static ArtifactMetadata Metadata(ArtifactRetention retention) =>
        ArtifactTestData.Metadata("x"u8.ToArray(), retention: retention);

    private static ArtifactReference Reference(ArtifactRetention retention, ExternalArtifactOwnership? external = null) => new(
        new ArtifactId(Guid.Parse("10000000-0000-0000-0000-000000000001")), new ArtifactVersion("1"), ArtifactTestData.Directory,
        ArtifactTestData.ProfileKey, new ArtifactProfileVersion(1), ArtifactTestData.Identity.TenantId, new ArtifactOwnerId("owner"),
        ArtifactTestData.Identity.PrincipalId, "text/plain", 1, new ArtifactIntegrity(new ContentHash("sha256:x"), _now),
        DataClassification.Internal, external is null ? ArtifactOwnershipKind.Session : ArtifactOwnershipKind.External,
        external is null ? ArtifactMutability.Immutable : ArtifactMutability.ExternallyManaged, retention, external, _now);
}
