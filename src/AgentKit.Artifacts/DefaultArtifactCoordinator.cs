// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

/// <summary>Validates bounded content and policy before delegating exact-grant effects to an artifact store.</summary>
public sealed class DefaultArtifactCoordinator: IArtifactCoordinator
{
    private readonly IArtifactStore _store;
    private readonly ISecurityAuthority _authority;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityIds;
    private readonly IIdentifierGenerator<ArtifactId> _artifactIds;
    private readonly IIdentifierGenerator<ArtifactPreparationId> _preparationIds;
    private readonly TimeProvider _time;
    private readonly AgentArtifactOptions _options;

    /// <summary>Initializes the coordinator over one selected backend and captured profile.</summary>
    /// <param name="store">The effecting artifact backend.</param><param name="authority">The system-wide security authority.</param>
    /// <param name="securityIds">The security request identity source.</param><param name="artifactIds">The artifact identity source.</param>
    /// <param name="preparationIds">The staging identity source.</param><param name="time">The deterministic clock.</param>
    /// <param name="options">The captured mechanics and profile.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A configured bound is invalid.</exception>
    public DefaultArtifactCoordinator(IArtifactStore store, ISecurityAuthority authority, IIdentifierGenerator<SecurityRequestId> securityIds, IIdentifierGenerator<ArtifactId> artifactIds, IIdentifierGenerator<ArtifactPreparationId> preparationIds, TimeProvider time, IOptions<AgentArtifactOptions> options)
    {
        ArgumentNullException.ThrowIfNull(store); ArgumentNullException.ThrowIfNull(authority); ArgumentNullException.ThrowIfNull(securityIds);
        ArgumentNullException.ThrowIfNull(artifactIds); ArgumentNullException.ThrowIfNull(preparationIds); ArgumentNullException.ThrowIfNull(time); ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.MaximumArtifactBytes); ArgumentOutOfRangeException.ThrowIfNegativeOrZero(options.Value.CopyBufferBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.Value.PreparationLifetime, TimeSpan.Zero);
        _store = store; _authority = authority; _securityIds = securityIds; _artifactIds = artifactIds; _preparationIds = preparationIds; _time = time; _options = options.Value;
    }

    /// <inheritdoc/>
    public async Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Metadata.DeclaredLength > _options.MaximumArtifactBytes)
        {
            return RejectPrepare(ArtifactFailureKind.LimitExceeded, "The artifact exceeds the configured byte limit.");
        }

        var content = await ReadBoundedAsync(request.Content, _options.MaximumArtifactBytes, _options.CopyBufferBytes, cancellationToken).ConfigureAwait(false);
        if (content is null)
        {
            return RejectPrepare(ArtifactFailureKind.LimitExceeded, "The artifact exceeds the configured byte limit.");
        }

        var bytes = content.Value;
        var hash = FileSecurityBinding.ContentFingerprint(bytes.AsSpan());
        if (bytes.Length != request.Metadata.DeclaredLength || hash != request.Metadata.DeclaredContentHash)
        {
            return RejectPrepare(ArtifactFailureKind.IntegrityMismatch, "Observed artifact length or integrity did not match its declaration.");
        }

        var artifactId = _artifactIds.Create();
        var preparationId = _preparationIds.Create();
        var version = new ArtifactVersion("1");
        var now = _time.GetUtcNow();
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation);
        var decision = await _authority.AuthorizeAsync(new SecurityRequest(
            _securityIds.Create(), scope, request.ToolCallId, request.Identity, _store.SecurityAudience,
            SecurityOperationKind.Artifact, SecurityEffect.Create,
            [ArtifactSecurityBinding.ArtifactResource(artifactId), ArtifactSecurityBinding.PreparationResource(preparationId)],
            ArtifactSecurityBinding.PrepareFingerprint(artifactId, preparationId, request.DirectoryId, request.Metadata), now.AddMinutes(1)), cancellationToken).ConfigureAwait(false);
        return decision is not SecurityAllowed allowed
            ? RejectPrepare(ArtifactFailureKind.Denied, decision is SecurityDenied denied ? denied.Denial.SafeMessage : "Artifact staging was not authorized.")
            : await _store.PrepareAsync(new ArtifactStorePrepareRequest(
            artifactId, preparationId, version, _options.ProfileKey, _options.ProfileVersion,
            request.Identity.TenantId, request.Identity.PrincipalId, request.DirectoryId, request.Metadata, bytes,
            now, now.Add(_options.PreparationLifetime), scope, request.Identity, allowed.Grant, request.IdempotencyKey), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation);
        var decision = await _authority.AuthorizeAsync(new SecurityRequest(
            _securityIds.Create(), scope, request.ToolCallId, request.Identity, _store.SecurityAudience,
            SecurityOperationKind.Artifact, SecurityEffect.CreateOrReplace,
            [ArtifactSecurityBinding.PreparationResource(request.PreparationId)], ArtifactSecurityBinding.FinalizeFingerprint(request.PreparationId),
            _time.GetUtcNow().AddMinutes(1)), cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            ? await _store.FinalizeAsync(new ArtifactStoreFinalizeRequest(request.PreparationId, scope, request.Identity, allowed.Grant, request.IdempotencyKey), cancellationToken).ConfigureAwait(false)
            : new ArtifactFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.Denied, decision is SecurityDenied denied ? denied.Denial.SafeMessage : "Artifact publication was not authorized."));
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation);
        var decision = await _authority.AuthorizeAsync(new SecurityRequest(
            _securityIds.Create(), scope, null, request.Identity, _store.SecurityAudience,
            SecurityOperationKind.Artifact, SecurityEffect.Delete,
            [ArtifactSecurityBinding.PreparationResource(request.PreparationId)], ArtifactSecurityBinding.AbortFingerprint(request.PreparationId, request.Reason),
            _time.GetUtcNow().AddMinutes(1)), cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            ? await _store.AbortAsync(new ArtifactStoreAbortRequest(request.PreparationId, request.Reason, scope, request.Identity, allowed.Grant, request.IdempotencyKey), cancellationToken).ConfigureAwait(false)
            : new ArtifactAbortRejected(new ArtifactFailure(ArtifactFailureKind.Denied, decision is SecurityDenied denied ? denied.Denial.SafeMessage : "Artifact abort was not authorized."));
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation);
        var decision = await _authority.AuthorizeAsync(new SecurityRequest(
            _securityIds.Create(), scope, request.ToolCallId, request.Identity, _store.SecurityAudience,
            SecurityOperationKind.Artifact, SecurityEffect.Observe,
            [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)], ArtifactSecurityBinding.ReadFingerprint(request.Reference),
            _time.GetUtcNow().AddMinutes(1)), cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            ? await _store.ReadAsync(new ArtifactStoreReadRequest(request.Reference, scope, request.Identity, allowed.Grant), cancellationToken).ConfigureAwait(false)
            : new ArtifactReadRejected(new ArtifactFailure(ArtifactFailureKind.Denied, decision is SecurityDenied denied ? denied.Denial.SafeMessage : "Artifact reading was not authorized."));
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Reference.Retention.LegalHold)
        {
            return new ArtifactDeleteRejected(new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "Artifact retention prohibits deletion."));
        }

        var scope = new SecurityAuthorizationScope(request.AgentId, request.SessionId, request.Correlation);
        var decision = await _authority.AuthorizeAsync(new SecurityRequest(
            _securityIds.Create(), scope, request.ToolCallId, request.Identity, _store.SecurityAudience,
            SecurityOperationKind.Artifact, SecurityEffect.Delete,
            [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)], ArtifactSecurityBinding.DeleteFingerprint(request.Reference),
            _time.GetUtcNow().AddMinutes(1)), cancellationToken).ConfigureAwait(false);
        return decision is SecurityAllowed allowed
            ? await _store.DeleteAsync(new ArtifactStoreDeleteRequest(request.Reference, scope, request.Identity, allowed.Grant, request.IdempotencyKey), cancellationToken).ConfigureAwait(false)
            : new ArtifactDeleteRejected(new ArtifactFailure(ArtifactFailureKind.Denied, decision is SecurityDenied denied ? denied.Denial.SafeMessage : "Artifact deletion was not authorized."));
    }

    private static async Task<ImmutableArray<byte>?> ReadBoundedAsync(Stream stream, long maximum, int bufferSize, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[bufferSize];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            if (output.Length + read > maximum)
            {
                return null;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return [.. output.ToArray()];
    }

    private static ArtifactPrepareRejected RejectPrepare(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));
}
