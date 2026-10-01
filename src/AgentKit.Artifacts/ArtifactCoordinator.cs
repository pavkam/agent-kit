// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Validates bounded content and policy, authorizes every effect, and delegates it to the routed artifact store.</summary>
/// <remarks>
/// <para>
/// One coordinator is bound to one key and one logical artifact profile. A write validates metadata against the profile, bounds and
/// integrity-checks the complete content, resolves retention, selects the routed backend, and only then requests a single-use grant
/// whose fingerprint binds the exact resolved evidence; the backend revalidates and consumes that grant before any state change.
/// Backend keys and locations never appear in a request, result, or reference.
/// </para>
/// <para>
/// Finalize, abort, and reconcile address a preparation by identity rather than directory, so they consult each distinct backend of
/// the retained profile revisions in deterministic order and stop at the first terminal answer. Reads and deletes route by the
/// reference's own directory and profile revision, so changing a route never strands an older reference. The coordinator never calls
/// the session, tool, or memory owner that holds a reference; reference commitment is coordinated by the caller.
/// </para>
/// </remarks>
internal sealed partial class ArtifactCoordinator: IArtifactCoordinator
{
    private static readonly ArtifactVersion _firstVersion = new("1");

    private readonly ComponentKey<IArtifactCoordinator> _key;
    private readonly ArtifactProfileSnapshot _profile;
    private readonly ImmutableArray<ArtifactProfileSnapshot> _profileVersions;
    private readonly IArtifactStoreSelector _stores;
    private readonly IArtifactIntegrityValidator _integrity;
    private readonly IArtifactRetentionPolicy _retention;
    private readonly ISecurityAuthoritySelector _authorities;
    private readonly IArtifactEventDispatcher _events;
    private readonly TimeProvider _time;
    private readonly IIdentifierGenerator<ArtifactId> _artifactIds;
    private readonly IIdentifierGenerator<ArtifactPreparationId> _preparationIds;
    private readonly IIdentifierGenerator<SecurityRequestId> _securityIds;
    private readonly AgentArtifactOptionsSnapshot _options;
    private readonly IArtifactReferenceCommitIntentStore? _intents;
    private readonly ILogger _logger;

    /// <summary>Initializes a coordinator bound to one key and one profile.</summary>
    /// <param name="key">The coordinator registration key.</param>
    /// <param name="profile">The current profile revision new writes bind to.</param>
    /// <param name="profileVersions">Every retained revision of the profile, including <paramref name="profile"/>, used to resolve older references.</param>
    /// <param name="stores">The selector that maps directories and backend keys to registered stores.</param>
    /// <param name="integrity">The content integrity validator.</param>
    /// <param name="retention">The retention policy.</param>
    /// <param name="securityAuthorities">The selector that activates the captured security authority for each request.</param>
    /// <param name="events">The deterministic lifecycle event dispatcher.</param>
    /// <param name="timeProvider">The deterministic clock.</param>
    /// <param name="artifactIds">The artifact identity source.</param>
    /// <param name="preparationIds">The staging identity source.</param>
    /// <param name="securityIds">The security request identity source.</param>
    /// <param name="options">The captured mechanics.</param>
    /// <param name="intents">The caller-owned reference-commit intent store, or <see langword="null"/> when reconciliation evidence is unavailable.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException">The key is blank, or the retained revisions do not contain the current profile.</exception>
    internal ArtifactCoordinator(
        ComponentKey<IArtifactCoordinator> key,
        ArtifactProfileSnapshot profile,
        ImmutableArray<ArtifactProfileSnapshot> profileVersions,
        IArtifactStoreSelector stores,
        IArtifactIntegrityValidator integrity,
        IArtifactRetentionPolicy retention,
        ISecurityAuthoritySelector securityAuthorities,
        IArtifactEventDispatcher events,
        TimeProvider timeProvider,
        IIdentifierGenerator<ArtifactId> artifactIds,
        IIdentifierGenerator<ArtifactPreparationId> preparationIds,
        IIdentifierGenerator<SecurityRequestId> securityIds,
        AgentArtifactOptionsSnapshot options,
        IArtifactReferenceCommitIntentStore? intents = null,
        ILogger<ArtifactCoordinator>? logger = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfDefaultOrEmpty(profileVersions);
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(integrity);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(securityAuthorities);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(artifactIds);
        ArgumentNullException.ThrowIfNull(preparationIds);
        ArgumentNullException.ThrowIfNull(securityIds);
        ArgumentNullException.ThrowIfNull(options);
        if (!profileVersions.Contains(profile))
        {
            throw new ArgumentException("The retained profile revisions must contain the current profile.", nameof(profileVersions));
        }

        _key = key;
        _profile = profile;
        _profileVersions = profileVersions;
        _stores = stores;
        _integrity = integrity;
        _retention = retention;
        _authorities = securityAuthorities;
        _events = events;
        _time = timeProvider;
        _artifactIds = artifactIds;
        _preparationIds = preparationIds;
        _securityIds = securityIds;
        _options = options;
        _intents = intents;
        _logger = logger ?? NullLogger<ArtifactCoordinator>.Instance;
    }

    /// <inheritdoc/>
    public Task<ArtifactPrepareResult> PrepareAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ArtifactObservability.ObserveAsync(
            Target(AgentKitActivityNames.ArtifactPrepare, "prepare", request.Authorization.Identity.TenantId),
            () => PrepareCoreAsync(request, cancellationToken),
            static result => result is ArtifactPrepareRejected rejected
                ? (ArtifactObservability.Name(rejected.Failure.Kind), false)
                : ("prepared", true));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactFinalizeResult> FinalizeAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<ArtifactFinalizeResult>(ArtifactObservability.ObserveAsync(
            Target(AgentKitActivityNames.ArtifactFinalize, "finalize", request.Authorization.Identity.TenantId, preparationId: request.PreparationId),
            () => FinalizeCoreAsync(request, cancellationToken).AsTask(),
            static result => result is ArtifactFinalizeRejected rejected
                ? (ArtifactObservability.Name(rejected.Failure.Kind), false)
                : ("finalized", true)));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactAbortResult> AbortAsync(ArtifactAbortRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<ArtifactAbortResult>(ArtifactObservability.ObserveAsync(
            Target(AgentKitActivityNames.ArtifactAbort, "abort", request.Authorization.Identity.TenantId, preparationId: request.PreparationId),
            () => AbortCoreAsync(request, cancellationToken).AsTask(),
            static result => result is ArtifactAbortRejected rejected
                ? (ArtifactObservability.Name(rejected.Failure.Kind), false)
                : ("aborted", true)));
    }

    /// <inheritdoc/>
    public Task<ArtifactReadResult> ReadAsync(ArtifactReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ArtifactObservability.ObserveAsync(
            Target(AgentKitActivityNames.ArtifactRead, "read", request.Authorization.Identity.TenantId, request.Reference.Id),
            () => ReadCoreAsync(request, cancellationToken),
            static result => result is ArtifactReadRejected rejected
                ? (ArtifactObservability.Name(rejected.Failure.Kind), false)
                : ("read", true));
    }

    /// <inheritdoc/>
    public ValueTask<ArtifactDeleteResult> DeleteAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new ValueTask<ArtifactDeleteResult>(ArtifactObservability.ObserveAsync(
            Target(AgentKitActivityNames.ArtifactDelete, "delete", request.Authorization.Identity.TenantId, request.Reference.Id),
            () => DeleteCoreAsync(request, cancellationToken).AsTask(),
            static result => result is ArtifactDeleteRejected rejected
                ? (ArtifactObservability.Name(rejected.Failure.Kind), false)
                : ("deleted", true)));
    }

    private async Task<ArtifactPrepareResult> PrepareCoreAsync(ArtifactPrepareRequest request, CancellationToken cancellationToken)
    {
        var metadata = request.Metadata;
        if (ValidateAgainstProfile(metadata) is { } profileFailure)
        {
            return new ArtifactPrepareRejected(profileFailure);
        }

        if (metadata.DeclaredLength > _options.MaximumArtifactBytes)
        {
            return RejectPrepare(ArtifactFailureKind.LimitExceeded, "The artifact exceeds the configured byte limit.");
        }

        var bytes = await ReadBoundedAsync(request.Content, _options.MaximumArtifactBytes, _options.CopyBufferBytes, cancellationToken).ConfigureAwait(false);
        if (bytes is null)
        {
            return RejectPrepare(ArtifactFailureKind.LimitExceeded, "The artifact exceeds the configured byte limit.");
        }

        var integrity = await _integrity.ValidateAsync(bytes.Value.AsMemory(), metadata.DeclaredLength, metadata.DeclaredContentHash, cancellationToken).ConfigureAwait(false);
        if (integrity is not ArtifactIntegrityVerified verified)
        {
            return new ArtifactPrepareRejected(
                (integrity as ArtifactIntegrityRejected)?.Failure
                ?? new ArtifactFailure(ArtifactFailureKind.IntegrityMismatch, "Observed artifact integrity could not be verified."));
        }

        var now = _time.GetUtcNow();
        var retention = await _retention.ResolveAsync(new ArtifactRetentionRequest(metadata, _profile.DefaultRetention, now), cancellationToken).ConfigureAwait(false);
        if (retention is not ArtifactRetentionAllowed allowedRetention)
        {
            return new ArtifactPrepareRejected(
                (retention as ArtifactRetentionRejected)?.Failure
                ?? new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "Retention could not be resolved."));
        }

        var resolved = new ArtifactMetadata(
            metadata.OwnerId, metadata.MediaType, metadata.DeclaredLength, metadata.DeclaredContentHash, metadata.Classification,
            metadata.Ownership, metadata.Mutability, allowedRetention.Retention, metadata.ExternalOwnership);
        var selection = await _stores.SelectAsync(_profile, request.DirectoryId, cancellationToken).ConfigureAwait(false);
        if (selection is not ArtifactStoreSelected selected)
        {
            return new ArtifactPrepareRejected(((ArtifactStoreUnavailable) selection).Failure);
        }

        var artifactId = _artifactIds.Create();
        var preparationId = _preparationIds.Create();
        var expiresAt = now.Add(_options.PreparationLifetime);
        var authorization = request.Authorization;
        var identity = authorization.Identity;
        var decision = await AuthorizeAsync(
            authorization,
            new SecurityRequest(
                _securityIds.Create(), authorization.Scope, request.ToolCallId, identity, authorization, selected.Store.SecurityAudience,
                SecurityOperationKind.Artifact, SecurityEffect.Create,
                [ArtifactSecurityBinding.ArtifactResource(artifactId), ArtifactSecurityBinding.PreparationResource(preparationId)],
                ArtifactSecurityBinding.PrepareFingerprint(
                    artifactId, preparationId, _firstVersion, _profile.Key, _profile.Version, identity.TenantId,
                    identity.PrincipalId, request.DirectoryId, resolved, verified.ContentHash, now, expiresAt),
                now.AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return RejectPrepare(ArtifactFailureKind.Denied, DenialMessage(decision, "Artifact staging was not authorized."));
        }

        var stored = await selected.Store.PrepareAsync(
            new ArtifactStorePrepareRequest(
                artifactId, preparationId, _firstVersion, _profile.Key, _profile.Version, identity.TenantId, identity.PrincipalId,
                request.DirectoryId, resolved, bytes.Value, verified.ContentHash, now, expiresAt, allowed.Grant, request.IdempotencyKey),
            cancellationToken).ConfigureAwait(false);
        if (stored is not ArtifactStorePrepared prepared)
        {
            return new ArtifactPrepareRejected(((ArtifactStorePrepareRejected) stored).Failure);
        }

        await PublishAsync(
            new ArtifactPreparedEvent(_key, identity.TenantId, _profile.Key, now, prepared.ArtifactId, prepared.PreparationId, prepared.Version),
            cancellationToken).ConfigureAwait(false);
        return new ArtifactPrepared(prepared.PreparationId, prepared.ArtifactId, prepared.Version, prepared.ExpiresAt);
    }

    private async ValueTask<ArtifactFinalizeResult> FinalizeCoreAsync(ArtifactFinalizeRequest request, CancellationToken cancellationToken)
    {
        var authorization = request.Authorization;
        var probe = await ProbeAsync(
            async (store, token) =>
            {
                var decision = await AuthorizeAsync(
                    authorization,
                    new SecurityRequest(
                        _securityIds.Create(), authorization.Scope, request.ToolCallId, authorization.Identity, authorization,
                        store.SecurityAudience, SecurityOperationKind.Artifact, SecurityEffect.CreateOrReplace,
                        [ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
                        ArtifactSecurityBinding.FinalizeFingerprint(request.PreparationId), _time.GetUtcNow().AddMinutes(1)),
                    token).ConfigureAwait(false);
                return decision is SecurityAllowed allowed
                    ? await store.FinalizeAsync(new ArtifactStoreFinalizeRequest(request.PreparationId, allowed.Grant, request.IdempotencyKey), token).ConfigureAwait(false)
                    : new ArtifactStoreFinalizeRejected(new ArtifactFailure(ArtifactFailureKind.Denied, DenialMessage(decision, "Artifact publication was not authorized.")));
            },
            static result => result is ArtifactStoreFinalizeRejected { Failure.Kind: ArtifactFailureKind.NotFound },
            static failure => new ArtifactStoreFinalizeRejected(failure),
            cancellationToken).ConfigureAwait(false);
        if (probe is ArtifactStoreFinalized finalized)
        {
            await PublishAsync(
                new ArtifactFinalizedEvent(
                    _key, authorization.Identity.TenantId, _profile.Key, _time.GetUtcNow(),
                    finalized.Reference.Id, finalized.Reference.Version, request.PreparationId),
                cancellationToken).ConfigureAwait(false);
            return new ArtifactFinalized(finalized.Reference);
        }

        return new ArtifactFinalizeRejected(((ArtifactStoreFinalizeRejected) probe).Failure);
    }

    private async ValueTask<ArtifactAbortResult> AbortCoreAsync(ArtifactAbortRequest request, CancellationToken cancellationToken)
    {
        var authorization = request.Authorization;
        var probe = await ProbeAsync(
            async (store, token) =>
            {
                var decision = await AuthorizeAsync(
                    authorization,
                    new SecurityRequest(
                        _securityIds.Create(), authorization.Scope, null, authorization.Identity, authorization,
                        store.SecurityAudience, SecurityOperationKind.Artifact, SecurityEffect.Delete,
                        [ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
                        ArtifactSecurityBinding.AbortFingerprint(request.PreparationId, request.Reason), _time.GetUtcNow().AddMinutes(1)),
                    token).ConfigureAwait(false);
                return decision is SecurityAllowed allowed
                    ? await store.AbortAsync(new ArtifactStoreAbortRequest(request.PreparationId, request.Reason, allowed.Grant, request.IdempotencyKey), token).ConfigureAwait(false)
                    : new ArtifactStoreAbortRejected(new ArtifactFailure(ArtifactFailureKind.Denied, DenialMessage(decision, "Artifact abort was not authorized.")));
            },
            static result => result is ArtifactStoreAbortRejected { Failure.Kind: ArtifactFailureKind.NotFound },
            static failure => new ArtifactStoreAbortRejected(failure),
            cancellationToken).ConfigureAwait(false);
        if (probe is ArtifactStoreAborted aborted)
        {
            await PublishAsync(
                new ArtifactAbortedEvent(
                    _key, authorization.Identity.TenantId, _profile.Key, _time.GetUtcNow(), request.PreparationId,
                    request.Reason, aborted.AlreadyAbsent),
                cancellationToken).ConfigureAwait(false);
            return new ArtifactAborted(aborted.AlreadyAbsent);
        }

        return new ArtifactAbortRejected(((ArtifactStoreAbortRejected) probe).Failure);
    }

    private async Task<ArtifactReadResult> ReadCoreAsync(ArtifactReadRequest request, CancellationToken cancellationToken)
    {
        var (store, routeFailure) = await RouteReferenceAsync(request.Reference, cancellationToken).ConfigureAwait(false);
        if (routeFailure is not null)
        {
            return new ArtifactReadRejected(routeFailure);
        }

        var authorization = request.Authorization;
        var decision = await AuthorizeAsync(
            authorization,
            new SecurityRequest(
                _securityIds.Create(), authorization.Scope, request.ToolCallId, authorization.Identity, authorization,
                store!.SecurityAudience, SecurityOperationKind.Artifact, SecurityEffect.Observe,
                [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)],
                ArtifactSecurityBinding.ReadFingerprint(request.Reference), _time.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return new ArtifactReadRejected(new ArtifactFailure(ArtifactFailureKind.Denied, DenialMessage(decision, "Artifact reading was not authorized.")));
        }

        var stored = await store.ReadAsync(new ArtifactStoreReadRequest(request.Reference, allowed.Grant), cancellationToken).ConfigureAwait(false);
        return stored is ArtifactStoreReadOpened opened
            ? new ArtifactReadOpened(opened.Reference, opened.Content)
            : new ArtifactReadRejected(((ArtifactStoreReadRejected) stored).Failure);
    }

    private async ValueTask<ArtifactDeleteResult> DeleteCoreAsync(ArtifactDeleteRequest request, CancellationToken cancellationToken)
    {
        var retention = await _retention.EvaluateDeletionAsync(request.Reference, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        if (retention is not ArtifactRetentionAllowed)
        {
            return new ArtifactDeleteRejected(
                (retention as ArtifactRetentionRejected)?.Failure
                ?? new ArtifactFailure(ArtifactFailureKind.RetentionConflict, "Artifact retention prohibits deletion."));
        }

        var (store, routeFailure) = await RouteReferenceAsync(request.Reference, cancellationToken).ConfigureAwait(false);
        if (routeFailure is not null)
        {
            return new ArtifactDeleteRejected(routeFailure);
        }

        var authorization = request.Authorization;
        var decision = await AuthorizeAsync(
            authorization,
            new SecurityRequest(
                _securityIds.Create(), authorization.Scope, request.ToolCallId, authorization.Identity, authorization,
                store!.SecurityAudience, SecurityOperationKind.Artifact, SecurityEffect.Delete,
                [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)],
                ArtifactSecurityBinding.DeleteFingerprint(request.Reference), _time.GetUtcNow().AddMinutes(1)),
            cancellationToken).ConfigureAwait(false);
        if (decision is not SecurityAllowed allowed)
        {
            return new ArtifactDeleteRejected(new ArtifactFailure(ArtifactFailureKind.Denied, DenialMessage(decision, "Artifact deletion was not authorized.")));
        }

        var stored = await store.DeleteAsync(new ArtifactStoreDeleteRequest(request.Reference, allowed.Grant, request.IdempotencyKey), cancellationToken).ConfigureAwait(false);
        if (stored is not ArtifactStoreDeleted deleted)
        {
            return new ArtifactDeleteRejected(((ArtifactStoreDeleteRejected) stored).Failure);
        }

        await PublishAsync(
            new ArtifactDeletedEvent(
                _key, authorization.Identity.TenantId, _profile.Key, _time.GetUtcNow(), request.Reference.Id,
                request.Reference.Version, deleted.AlreadyAbsent),
            cancellationToken).ConfigureAwait(false);
        return new ArtifactDeleted(deleted.AlreadyAbsent);
    }

    private ArtifactFailure? ValidateAgainstProfile(ArtifactMetadata metadata)
    {
        return !_profile.AllowedMutability.Contains(metadata.Mutability)
            ? new ArtifactFailure(ArtifactFailureKind.Denied, "The selected profile does not admit this artifact mutability.")
            : metadata.ExternalOwnership is not null && !_profile.AllowExternalOwnership
                ? new ArtifactFailure(ArtifactFailureKind.Denied, "The selected profile does not admit externally owned artifacts.")
                : _options.RequireDeclaredContentHash && metadata.DeclaredContentHash is null
                    ? new ArtifactFailure(ArtifactFailureKind.IntegrityMismatch, "The selected coordinator requires a declared content hash.")
                    : null;
    }

    private async ValueTask<(IArtifactStore? Store, ArtifactFailure? Failure)> RouteReferenceAsync(ArtifactReference reference, CancellationToken cancellationToken)
    {
        if (reference.ProfileKey != _profile.Key)
        {
            return (null, new ArtifactFailure(ArtifactFailureKind.NotFound, "The artifact reference belongs to another profile."));
        }

        var snapshot = _profileVersions.FirstOrDefault(version => version.Version == reference.ProfileVersion);
        if (snapshot is null)
        {
            return (null, new ArtifactFailure(ArtifactFailureKind.Unavailable, "The profile revision that created the artifact reference is not retained."));
        }

        var selection = await _stores.SelectAsync(snapshot, reference.DirectoryId, cancellationToken).ConfigureAwait(false);
        return selection is ArtifactStoreSelected selected
            ? (selected.Store, null)
            : (null, ((ArtifactStoreUnavailable) selection).Failure);
    }

    /// <summary>Consults each distinct backend in deterministic order until one gives a terminal answer for a preparation.</summary>
    /// <typeparam name="TStoreResult">The store result family.</typeparam>
    /// <param name="invoke">Authorizes and runs the operation against one store.</param>
    /// <param name="isNotFound">Identifies the only non-terminal answer, which moves on to the next backend.</param>
    /// <param name="unavailable">Creates a rejection when a backend could not be consulted.</param>
    /// <param name="cancellationToken">Cancels before the next backend is consulted.</param>
    /// <returns>The first terminal answer; otherwise unavailable when any backend could not answer, else the last not-found answer.</returns>
    private async ValueTask<TStoreResult> ProbeAsync<TStoreResult>(
        Func<IArtifactStore, CancellationToken, ValueTask<TStoreResult>> invoke,
        Func<TStoreResult, bool> isNotFound,
        Func<ArtifactFailure, TStoreResult> unavailable,
        CancellationToken cancellationToken)
        where TStoreResult : class
    {
        TStoreResult? missing = null;
        ArtifactFailure? unreachable = null;
        foreach (var backend in Backends())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selection = await _stores.SelectBackendAsync(backend, cancellationToken).ConfigureAwait(false);
            if (selection is not ArtifactStoreSelected selected)
            {
                unreachable ??= ((ArtifactStoreUnavailable) selection).Failure;
                continue;
            }

            var result = await invoke(selected.Store, cancellationToken).ConfigureAwait(false);
            if (!isNotFound(result))
            {
                return result;
            }

            missing = result;
        }

        Debug.Assert(missing is not null || unreachable is not null, "A profile always routes at least one backend.");
        return unreachable is not null ? unavailable(unreachable) : missing!;
    }

    private ImmutableArray<ArtifactBackendKey> Backends() =>
    [
        .. _profileVersions
            .OrderByDescending(static version => version.Version.Value)
            .SelectMany(static version => version.Backends())
            .Distinct(),
    ];

    private async ValueTask<SecurityDecision?> AuthorizeAsync(
        SecurityAuthorizationContext authorization,
        SecurityRequest request,
        CancellationToken cancellationToken)
    {
        var activated = await _authorities.SelectAsync(authorization, cancellationToken).ConfigureAwait(false);
        return activated is SecurityAuthoritySelected selected && selected.Authorization == authorization
            ? await selected.Authority.AuthorizeAsync(request, hooks: null, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async ValueTask PublishAsync(ArtifactEvent artifactEvent, CancellationToken cancellationToken)
    {
        try
        {
            await _events.PublishAsync(_key, artifactEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A sink failure never changes a committed lifecycle outcome; the dispatcher already isolates and records it.
        }
    }

    private ArtifactObservationTarget Target(
        string activityName,
        string operation,
        TenantId tenantId,
        ArtifactId? artifactId = null,
        ArtifactPreparationId? preparationId = null) =>
        new(_logger, _time, _key, _profile.Key, activityName, operation, tenantId, artifactId, preparationId);

    private static string DenialMessage(SecurityDecision? decision, string fallback) =>
        decision is SecurityDenied denied ? denied.Denial.SafeMessage : fallback;

    private static ArtifactPrepareRejected RejectPrepare(ArtifactFailureKind kind, string message) => new(new ArtifactFailure(kind, message));

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
}
