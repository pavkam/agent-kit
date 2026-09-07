// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Tests;

internal sealed class RecordingArtifactStore: IArtifactStore
{
    internal List<ArtifactStorePrepareRequest> PrepareRequests { get; } = [];
    internal List<ArtifactStoreFinalizeRequest> FinalizeRequests { get; } = [];
    internal List<ArtifactStoreAbortRequest> AbortRequests { get; } = [];
    internal List<ArtifactStoreReadRequest> ReadRequests { get; } = [];
    internal List<ArtifactStoreDeleteRequest> DeleteRequests { get; } = [];

    public ComponentId SecurityAudience { get; } = new("test.artifact-store");

    public ValueTask<ArtifactPrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default)
    {
        PrepareRequests.Add(request);
        return ValueTask.FromResult<ArtifactPrepareResult>(new ArtifactPrepared(request.PreparationId, request.ArtifactId, request.Version, request.ExpiresAt));
    }

    public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default)
    {
        FinalizeRequests.Add(request);
        return ValueTask.FromResult<ArtifactFinalizeResult>(new ArtifactFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "test")));
    }

    public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default)
    {
        AbortRequests.Add(request);
        return ValueTask.FromResult<ArtifactAbortResult>(new ArtifactAborted(false));
    }

    public ValueTask<ArtifactReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default)
    {
        ReadRequests.Add(request);
        return ValueTask.FromResult<ArtifactReadResult>(new ArtifactReadRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "test")));
    }

    public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default)
    {
        DeleteRequests.Add(request);
        return ValueTask.FromResult<ArtifactDeleteResult>(new ArtifactDeleted(false));
    }
}

internal sealed class RecordingSecurityAuthority(bool allow = true): ISecurityAuthority
{
    internal List<SecurityRequest> Requests { get; } = [];
    internal List<SecurityGrant> IssuedGrants { get; } = [];

    public ValueTask<SecurityDecision> AuthorizeAsync(SecurityRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        if (!allow)
        {
            return ValueTask.FromResult<SecurityDecision>(
                new SecurityDenied(request.Id, new SecurityPolicyVersion(1), new SecurityDenial("test.denied", "Denied.")));
        }

        var grant = new SecurityGrant(
                new GrantId(Guid.Parse("80000000-0000-0000-0000-000000000008")), request.Id, request.Scope,
                request.Identity, request.Audience, request.Kind, request.Effect, request.Resources,
                request.InputFingerprint, new SecurityPolicyVersion(1), new SecurityRevocationVersion(1),
                ArtifactTestData.Now, request.Deadline, 1);
        IssuedGrants.Add(grant);
        return ValueTask.FromResult<SecurityDecision>(new SecurityAllowed(request.Id, new SecurityPolicyVersion(1), grant));
    }
}

internal sealed class FixedIdentifierGenerator<T>(T value): IIdentifierGenerator<T>
    where T : struct
{
    public T Create() => value;
}

internal sealed class FixedTimeProvider: TimeProvider
{
    public override DateTimeOffset GetUtcNow() => ArtifactTestData.Now;
}

internal sealed class RecordingArtifactCoordinator: IArtifactCoordinator
{
    internal List<ArtifactPrepareRequest> PrepareRequests { get; } = [];
    internal List<ArtifactFinalizeRequest> FinalizeRequests { get; } = [];
    internal List<ArtifactAbortRequest> AbortRequests { get; } = [];
    internal ImmutableArray<byte> PreparedContent { get; private set; } = [];
    internal ArtifactPrepareResult PrepareResult { get; set; } = new ArtifactPrepared(
        ArtifactTestData.PreparationId, ArtifactTestData.ArtifactId, new ArtifactVersion("1"), ArtifactTestData.Now.AddMinutes(5));
    internal ArtifactFinalizeResult FinalizeResult { get; set; } = new ArtifactFinalized(ArtifactTestData.CreateReference());

    public Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default)
    {
        PrepareRequests.Add(request);
        using var copy = new MemoryStream();
        request.Content.CopyTo(copy);
        PreparedContent = [.. copy.ToArray()];
        return Task.FromResult(PrepareResult);
    }

    public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default)
    {
        FinalizeRequests.Add(request);
        return ValueTask.FromResult(FinalizeResult);
    }

    public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default)
    {
        AbortRequests.Add(request);
        return ValueTask.FromResult<ArtifactAbortResult>(new ArtifactAborted(false));
    }

    public ValueTask<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ArtifactReadResult>(new ArtifactReadRejected(new ArtifactFailure(ArtifactFailureKind.NotFound, "test")));

    public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult<ArtifactDeleteResult>(new ArtifactDeleted(false));
}
