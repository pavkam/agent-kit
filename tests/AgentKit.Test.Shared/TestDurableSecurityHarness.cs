// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.TestSupport;

/// <summary>Issues deterministic exact journal grants and observes required audit for journal tests.</summary>
/// <remarks>
/// The harness enforces the same exact-binding rule a real grant store must: a grant whose scope, identity,
/// authorization, audience, kind, effect, resources, request digest, or revocation version differs from the
/// enforcement evidence the journal recomputed is a mismatch, never a consumption. Each grant carries a single use,
/// so a replayed write is exhausted rather than authorized twice. Individual knobs let a test make consumption or
/// audit fail without weakening that rule for every other test.
/// </remarks>
public sealed class TestDurableSecurityHarness: ISecurityGrantStore, ISecurityAuditDispatcher
{
    private readonly HashSet<GrantId> _consumed = [];
    private readonly List<SecurityAuditRecord> _records = [];
    private long _nextIdentity;

    /// <summary>Gets or sets the status every consumption reports instead of validating the exact binding.</summary>
    /// <value>Null to validate normally, or a status such as <see cref="GrantConsumptionStatus.Revoked"/> to force.</value>
    public GrantConsumptionStatus? ForcedConsumptionStatus { get; set; }

    /// <summary>Gets or sets whether consumption returns a receipt bound to a different intent.</summary>
    /// <value>True to return a receipt whose identity does not match the presented intent.</value>
    public bool ForgeIntentReceipt { get; set; }

    /// <summary>Gets or sets whether required audit delivery refuses the record.</summary>
    /// <value>True to return <see cref="SecurityAuditUnavailable"/> for every dispatch.</value>
    public bool RejectAudit { get; set; }

    /// <summary>Gets or sets whether required audit delivery throws instead of answering.</summary>
    /// <value>True to make the dispatcher unavailable.</value>
    public bool FailAudit { get; set; }

    /// <summary>Gets or sets whether the grant store throws instead of answering.</summary>
    /// <value>True to make the authority unavailable.</value>
    public bool FailGrantStore { get; set; }

    /// <summary>Gets every audit record the journal dispatched, in dispatch order.</summary>
    /// <value>A live view of accepted and rejected dispatch attempts.</value>
    public IReadOnlyList<SecurityAuditRecord> Records => _records;

    /// <summary>Gets how many grant uses this harness consumed.</summary>
    /// <value>The count of distinct grants whose single use was consumed.</value>
    public int ConsumedCount => _consumed.Count;

    /// <summary>Wraps one exact journal request in a single-use grant that matches what the journal will recompute.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="request">The exact request the journal will receive.</param>
    /// <param name="journalKey">The journal the grant targets.</param>
    /// <param name="audience">The journal's consuming component identity.</param>
    /// <param name="address">The durable coordinates the effect targets.</param>
    /// <param name="authorization">The capture the effect runs under.</param>
    /// <param name="fingerprint">The canonical digest over <paramref name="request"/>.</param>
    /// <param name="kind">The protected operation kind.</param>
    /// <param name="effect">The protected effect.</param>
    /// <param name="requiredFence">The ownership generation the intent requires, or null for an unfenced read.</param>
    /// <returns>A protected request the journal accepts exactly once.</returns>
    public AuthorizedDurableRequest<TRequest> Authorize<TRequest>(
        TRequest request,
        DurableJournalKey journalKey,
        ComponentId audience,
        DurableOperationAddress address,
        SecurityAuthorizationContext authorization,
        InputFingerprint fingerprint,
        SecurityOperationKind kind,
        SecurityEffect effect,
        FencingToken? requiredFence)
        where TRequest : class
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(authorization);
        var grant = new SecurityGrant(
            new GrantId(NextGuid()),
            new SecurityRequestId(NextGuid()),
            authorization.Scope,
            authorization.Identity,
            authorization,
            audience,
            kind,
            effect,
            [DurableJournalSecurityBinding.Resource(journalKey, address)],
            fingerprint,
            authorization.PolicySnapshot.Version,
            new SecurityRevocationVersion(1),
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddYears(100),
            allowedUses: 1);
        var intent = new SecurityEnforcementIntent(new SecurityEnforcementIntentId(NextGuid()), requiredFence);
        return new AuthorizedDurableRequest<TRequest>(request, journalKey, grant, intent);
    }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        if (FailGrantStore)
        {
            throw new InvalidOperationException("The test grant store is unavailable.");
        }
        if (ForcedConsumptionStatus is { } forced)
        {
            return ValueTask.FromResult(Refused(forced, "The test grant store forced this status."));
        }

        var matches = grant.Scope == enforcement.Scope
            && grant.Identity == enforcement.Identity
            && grant.Authorization == enforcement.Authorization
            && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind
            && grant.Effect == enforcement.Effect
            && grant.Resources.SequenceEqual(enforcement.Resources)
            && grant.InputFingerprint == enforcement.InputFingerprint
            && grant.RevocationVersion == enforcement.RevocationVersion;
        if (!matches)
        {
            return ValueTask.FromResult(Refused(GrantConsumptionStatus.Mismatch, "The exact grant binding did not match."));
        }

        if (!_consumed.Add(grant.Id))
        {
            return ValueTask.FromResult(Refused(GrantConsumptionStatus.Exhausted, "The grant use was already consumed."));
        }

        var intentId = ForgeIntentReceipt
            ? new SecurityEnforcementIntentId(NextGuid())
            : intent.Id;
        return ValueTask.FromResult(new GrantConsumptionResult(
            GrantConsumptionStatus.Consumed,
            0,
            "The exact grant use was consumed.",
            new SecurityEnforcementIntentReceipt(
                intentId,
                grant.Id,
                grant.RequestId,
                enforcement,
                intent.RequiredFence,
                SecurityEnforcementBinding.Fingerprint(enforcement, intent),
                DateTimeOffset.UnixEpoch)));
    }

    private static GrantConsumptionResult Refused(GrantConsumptionStatus status, string message) =>
        new(status, 0, message, null);

    /// <inheritdoc/>
    public ValueTask<GrantRevocationResult> RevokeAsync(
        GrantId grantId,
        RevocationReason reason,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reason);
        return ValueTask.FromResult<GrantRevocationResult>(new GrantRevoked(grantId, reason));
    }

    /// <inheritdoc/>
    public ValueTask<SecurityAuditDispatchResult> DispatchAsync(
        SecurityAuditRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        cancellationToken.ThrowIfCancellationRequested();
        if (FailAudit)
        {
            throw new InvalidOperationException("The test audit dispatcher is unavailable.");
        }

        _records.Add(record);
        return ValueTask.FromResult<SecurityAuditDispatchResult>(RejectAudit
            ? new SecurityAuditUnavailable("The test audit dispatcher refused the record.")
            : new SecurityAuditAccepted());
    }

    private Guid NextGuid()
    {
        var value = Interlocked.Increment(ref _nextIdentity);
        Span<byte> bytes = stackalloc byte[16];
        _ = BitConverter.TryWriteBytes(bytes, value);
        bytes[15] = 1;
        return new Guid(bytes);
    }
}
