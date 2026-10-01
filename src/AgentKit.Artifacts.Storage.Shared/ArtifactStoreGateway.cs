// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Artifacts.Storage;

using Microsoft.Extensions.Logging.Abstractions;

/// <summary>Is the single protected entry point every first-party artifact store adapter shares.</summary>
/// <remarks>
/// For each operation the gateway validates the request, atomically validates and consumes the exact single-use grant, and only
/// then hands the authorized request to the adapter's backend. Observation wraps the whole operation and never changes its result.
/// </remarks>
internal sealed class ArtifactStoreGateway: IArtifactStore
{
    private readonly string _adapter;
    private readonly IArtifactStoreBackend _backend;
    private readonly ArtifactStoreEnforcement _enforcement;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    /// <summary>Initializes a gateway over one adapter backend.</summary>
    /// <param name="adapter">The bounded adapter label, such as <c>sqlite</c>.</param>
    /// <param name="audience">The store component identity grants must name.</param>
    /// <param name="backend">The adapter backend that executes authorized operations.</param>
    /// <param name="grants">The authoritative grant store.</param>
    /// <param name="intentIds">The allocator of fresh enforcement-intent identities.</param>
    /// <param name="time">The clock stamping operations and observational duration.</param>
    /// <param name="logger">The optional content-free logger.</param>
    /// <exception cref="ArgumentNullException">A required dependency is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="adapter"/> or <paramref name="audience"/> is blank.</exception>
    internal ArtifactStoreGateway(
        string adapter,
        ComponentId audience,
        IArtifactStoreBackend backend,
        ISecurityGrantStore grants,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        TimeProvider time,
        ILogger? logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapter);
        ArgumentException.ThrowIfNullOrWhiteSpace(audience.Value, nameof(audience));
        ArgumentNullException.ThrowIfNull(backend);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(time);
        _adapter = adapter;
        SecurityAudience = audience;
        _backend = backend;
        _enforcement = new ArtifactStoreEnforcement(grants, intentIds, audience);
        _time = time;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <inheritdoc/>
    public ComponentId SecurityAudience { get; }

    /// <inheritdoc/>
    public Task<ArtifactStorePrepareResult> PrepareAsync(ArtifactStorePrepareRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(ArtifactStoreOperationKind.Prepare, request.Identity.TenantId, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityEffect.Create,
                [ArtifactSecurityBinding.ArtifactResource(request.ArtifactId), ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
                ArtifactSecurityBinding.PrepareFingerprint(
                    request.ArtifactId, request.PreparationId, request.Version, request.ProfileKey, request.ProfileVersion,
                    request.TenantId, request.CreatedBy, request.DirectoryId, request.Metadata, request.ContentHash,
                    request.CreatedAt, request.ExpiresAt), cancellationToken).ConfigureAwait(false);
            return denial is not null
                ? new ArtifactStorePrepareRejected(denial)
                : await _backend.PrepareAsync(request, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }, static result => (result as ArtifactStorePrepareRejected)?.Failure);
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreFinalizeResult> FinalizeAsync(ArtifactStoreFinalizeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Observe(ArtifactStoreOperationKind.Finalize, request.Identity.TenantId, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityEffect.CreateOrReplace,
                [ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
                ArtifactSecurityBinding.FinalizeFingerprint(request.PreparationId), cancellationToken).ConfigureAwait(false);
            return denial is not null
                ? new ArtifactStoreFinalizeRejected(denial)
                : await _backend.FinalizeAsync(request, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }, static result => (result as ArtifactStoreFinalizeRejected)?.Failure).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreAbortResult> AbortAsync(ArtifactStoreAbortRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Observe(ArtifactStoreOperationKind.Abort, request.Identity.TenantId, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityEffect.Delete,
                [ArtifactSecurityBinding.PreparationResource(request.PreparationId)],
                ArtifactSecurityBinding.AbortFingerprint(request.PreparationId, request.Reason), cancellationToken).ConfigureAwait(false);
            return denial is not null
                ? new ArtifactStoreAbortRejected(denial)
                : await _backend.AbortAsync(request, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }, static result => (result as ArtifactStoreAbortRejected)?.Failure).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<ArtifactStoreReadResult> ReadAsync(ArtifactStoreReadRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Observe(ArtifactStoreOperationKind.Read, request.Identity.TenantId, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityEffect.Observe,
                [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)],
                ArtifactSecurityBinding.ReadFingerprint(request.Reference), cancellationToken).ConfigureAwait(false);
            return denial is not null
                ? new ArtifactStoreReadRejected(denial)
                : await _backend.ReadAsync(request, cancellationToken).ConfigureAwait(false);
        }, static result => (result as ArtifactStoreReadRejected)?.Failure);
    }

    /// <inheritdoc/>
    public async ValueTask<ArtifactStoreDeleteResult> DeleteAsync(ArtifactStoreDeleteRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await Observe(ArtifactStoreOperationKind.Delete, request.Identity.TenantId, async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var denial = await _enforcement.ConsumeAsync(
                request.Grant, SecurityEffect.Delete,
                [ArtifactSecurityBinding.ArtifactResource(request.Reference.Id)],
                ArtifactSecurityBinding.DeleteFingerprint(request.Reference), cancellationToken).ConfigureAwait(false);
            return denial is not null
                ? new ArtifactStoreDeleteRejected(denial)
                : await _backend.DeleteAsync(request, _time.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        }, static result => (result as ArtifactStoreDeleteRejected)?.Failure).ConfigureAwait(false);
    }

    private Task<TResult> Observe<TResult>(
        ArtifactStoreOperationKind kind,
        TenantId tenantId,
        Func<Task<TResult>> operation,
        Func<TResult, ArtifactFailure?> failureOf) =>
        ArtifactStoreObservation.ObserveAsync(_logger, _time, _adapter, kind, tenantId, operation, failureOf);
}
