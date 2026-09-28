// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.Storage;

/// <summary>Provides the grant-consuming, audited ingress every persistent durable journal shares.</summary>
/// <remarks>
/// <para>
/// Journal access is a protected operation. Each write consumes its single-use grant and completes required audit
/// before any record is persisted, and an authorized evidence read is audited but unfenced so a recovering worker can
/// learn what happened before it seeks ownership.
/// </para>
/// <para>
/// The enforcement is deliberately identical across storage families: only the journal key and the consuming audience
/// differ, and both are supplied at construction so a composition cannot produce a journal that silently skips grant
/// consumption or audit. The boundary fails closed. A mismatched journal key, a fence the request does not carry, an
/// authorization capture that cannot describe the address, an unconsumed grant, invalid intent evidence, or unavailable
/// required audit all deny before anything is written. Cancellation propagates; every other prerequisite failure
/// becomes a denial rather than an exception, because an unavailable authority must never look like a committed write.
/// </para>
/// </remarks>
internal sealed class DurableJournalEnforcement
{
    private readonly DurableJournalKey _key;
    private readonly ComponentId _audience;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly ISecurityGrantStore _grants;
    private readonly TimeProvider _timeProvider;

    /// <summary>Binds the ingress to one journal registration and its required security collaborators.</summary>
    /// <param name="key">The exact registration key every authorized request must target.</param>
    /// <param name="audience">The stable component identity every journal grant must have been minted for.</param>
    /// <param name="auditRecordIds">The non-null generator for each required audit record's stable identity.</param>
    /// <param name="auditDispatcher">The non-null required dispatcher that must accept every consumed access intent.</param>
    /// <param name="grants">The non-null authoritative store that validates and consumes each exact single-use grant.</param>
    /// <param name="timeProvider">The non-null injected clock used for audit instants.</param>
    /// <exception cref="ArgumentNullException"><paramref name="auditRecordIds"/>, <paramref name="auditDispatcher"/>, <paramref name="grants"/>, or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="key"/> or <paramref name="audience"/> carries no key text.</exception>
    internal DurableJournalEnforcement(
        DurableJournalKey key,
        ComponentId audience,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        ISecurityAuditDispatcher auditDispatcher,
        ISecurityGrantStore grants,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key.Value, nameof(key));
        ArgumentException.ThrowIfNullOrWhiteSpace(audience.Value, nameof(audience));
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(grants);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _key = key;
        _audience = audience;
        _auditRecordIds = auditRecordIds;
        _auditDispatcher = auditDispatcher;
        _grants = grants;
        _timeProvider = timeProvider;
    }

    /// <summary>Recomputes enforcement evidence, consumes the single-use grant, and completes required audit.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="wrapper">The non-null protected request carrying the grant and stable enforcement intent.</param>
    /// <param name="address">The non-null operation coordinates the concrete effect targets.</param>
    /// <param name="authorization">The non-null authorization capture the effect must run under.</param>
    /// <param name="requiredFence">The ownership generation the effect must present, or <see langword="null"/> for an unfenced authorized read.</param>
    /// <param name="fingerprint">The canonical digest over the complete request.</param>
    /// <param name="kind">The protected operation kind this call enforces.</param>
    /// <param name="effect">The protected effect this call enforces.</param>
    /// <param name="cancellationToken">Cancels before the grant is consumed or audit is dispatched.</param>
    /// <returns>A receipt when the grant was consumed and audit accepted, otherwise a content-free denial reason; exactly one of the two is non-null.</returns>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was signalled.</exception>
    internal async ValueTask<(DurableJournalEnforcementReceipt? Receipt, string? Denial)> EnforceAsync<TRequest>(
        AuthorizedDurableRequest<TRequest> wrapper,
        DurableOperationAddress address,
        SecurityAuthorizationContext authorization,
        FencingToken? requiredFence,
        InputFingerprint fingerprint,
        SecurityOperationKind kind,
        SecurityEffect effect,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        Debug.Assert(wrapper is not null, "A protected journal request is required.");
        Debug.Assert(address is not null, "Concrete operation coordinates are required.");
        Debug.Assert(authorization is not null, "A captured authorization is required.");

        if (wrapper.JournalKey != _key)
        {
            return (null, "The grant targets a different durable journal.");
        }
        if (wrapper.Intent.RequiredFence != requiredFence)
        {
            return (null, "The enforcement intent fence differs from the durable journal request.");
        }
        if (!DescribesAddress(authorization, address))
        {
            return (null, "The captured authorization cannot describe the durable operation address.");
        }

        // The audit instant is read before the grant is consumed. A clock failure must surface as a failed attempt
        // that consumed nothing, not as a denial after the grant's single use was already spent.
        var auditedAt = _timeProvider.GetUtcNow();
        var enforcement = new SecurityEnforcementRequest(
            authorization.Scope,
            authorization.Identity,
            authorization,
            _audience,
            kind,
            effect,
            [DurableJournalSecurityBinding.Resource(_key, address)],
            fingerprint,
            wrapper.Grant.RevocationVersion);

        try
        {
            var consumption = await _grants
                .ValidateAndConsumeAsync(wrapper.Grant, enforcement, wrapper.Intent, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (consumption.Status != GrantConsumptionStatus.Consumed)
            {
                return (null, consumption.SafeMessage);
            }
            if (consumption.IntentReceipt is not { } receipt || !ReceiptMatches(receipt, wrapper, enforcement))
            {
                return (null, "The grant store returned invalid enforcement-intent evidence.");
            }

            var record = CreateAuditRecord(wrapper.Grant, enforcement, auditedAt);
            var audit = await _auditDispatcher.DispatchAsync(record, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return audit is SecurityAuditAccepted
                ? (new DurableJournalEnforcementReceipt(
                    wrapper.Grant.Id,
                    wrapper.Intent.Id,
                    record.Id,
                    receipt.ConsumedAt), null)
                : (null, "Required audit delivery is unavailable for the durable journal operation.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return (null, "A durable journal authorization prerequisite is unavailable.");
        }
    }

    /// <summary>Determines whether a captured authorization scope describes exactly one durable address.</summary>
    /// <param name="authorization">The captured authorization whose scope and correlation are compared.</param>
    /// <param name="address">The durable coordinates the effect targets.</param>
    /// <returns><see langword="true"/> only when agent, session, operation, run, and optional turn all match exactly.</returns>
    /// <remarks>
    /// This is the same rule <see cref="DurableOperationBinding"/> enforces, applied to an evidence read whose request
    /// carries only an address. It accepts in-run work and after-run follow-up whose address retains the causal run
    /// with no turn, and refuses sessionless authorization.
    /// </remarks>
    private static bool DescribesAddress(SecurityAuthorizationContext authorization, DurableOperationAddress address)
    {
        Debug.Assert(authorization is not null, "A captured authorization is required.");
        Debug.Assert(address is not null, "Concrete operation coordinates are required.");
        var scope = authorization.Scope;
        return scope.SessionId is { } sessionId
            && scope.AgentId == address.AgentId
            && sessionId == address.SessionId
            && scope.Correlation.OperationId == address.OperationId
            && scope.Correlation switch
            {
                InRunOperationCorrelation inRun => address.RunId == inRun.RunId && address.TurnId == inRun.TurnId,
                AfterRunOperationCorrelation afterRun => address.RunId == afterRun.CausalRunId && address.TurnId is null,
                _ => false,
            };
    }

    /// <summary>Verifies that a grant-store receipt describes exactly this request's consumption.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="receipt">The receipt the grant store persisted atomically with consumption.</param>
    /// <param name="wrapper">The protected request whose grant and intent were presented.</param>
    /// <param name="enforcement">The evidence the journal recomputed immediately before consumption.</param>
    /// <returns><see langword="true"/> only when every identity, fence, and effect fingerprint matches exactly.</returns>
    private static bool ReceiptMatches<TRequest>(
        SecurityEnforcementIntentReceipt receipt,
        AuthorizedDurableRequest<TRequest> wrapper,
        SecurityEnforcementRequest enforcement)
        where TRequest : class
    {
        Debug.Assert(receipt is not null, "A grant-store intent receipt is required.");
        Debug.Assert(wrapper is not null, "A protected journal request is required.");
        Debug.Assert(enforcement is not null, "Recomputed enforcement evidence is required.");
        return receipt.IntentId == wrapper.Intent.Id
            && receipt.GrantId == wrapper.Grant.Id
            && receipt.RequestId == wrapper.Grant.RequestId
            && receipt.RequiredFence == wrapper.Intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, wrapper.Intent)
            && EnforcementMatches(receipt.Enforcement, enforcement);
    }

    /// <summary>Compares retained and recomputed enforcement evidence field by field.</summary>
    /// <param name="actual">The evidence the grant store retained with the consumption.</param>
    /// <param name="expected">The evidence the journal recomputed for this exact request.</param>
    /// <returns><see langword="true"/> only when scope, identity, authorization, audience, kind, effect, digest, revocation, and resources match.</returns>
    private static bool EnforcementMatches(SecurityEnforcementRequest actual, SecurityEnforcementRequest expected)
    {
        Debug.Assert(actual is not null, "Retained enforcement evidence is required.");
        Debug.Assert(expected is not null, "Recomputed enforcement evidence is required.");
        return actual.Scope == expected.Scope
            && actual.Identity == expected.Identity
            && actual.Authorization == expected.Authorization
            && actual.Audience == expected.Audience
            && actual.Kind == expected.Kind
            && actual.Effect == expected.Effect
            && actual.InputFingerprint == expected.InputFingerprint
            && actual.RevocationVersion == expected.RevocationVersion
            && actual.Resources.SequenceEqual(expected.Resources);
    }

    /// <summary>Builds the required content-free audit record for one consumed journal grant.</summary>
    /// <param name="grant">The grant whose single use was consumed.</param>
    /// <param name="enforcement">The recomputed evidence bound to the consumption.</param>
    /// <param name="auditedAt">The instant read before consumption, so a clock failure cannot strand a spent grant.</param>
    /// <returns>A record carrying only redacted, bounded values.</returns>
    /// <remarks>
    /// The record fingerprints the protected resource rather than naming it, so an operation address never reaches an
    /// audit sink as readable text. No payload, state, or credential is included.
    /// </remarks>
    private SecurityAuditRecord CreateAuditRecord(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        DateTimeOffset auditedAt)
    {
        Debug.Assert(grant is not null, "A consumed grant is required for audit.");
        Debug.Assert(enforcement is not null, "Recomputed enforcement evidence is required for audit.");
        return new SecurityAuditRecord(
            _auditRecordIds.Create(),
            enforcement.Scope,
            grant.RequestId,
            grant.Id,
            null,
            SecurityAuditEventKind.GrantConsumptionIntent,
            SecurityAuditOutcome.Accepted,
            grant.PolicyVersion,
            ImmutableDictionary<string, RedactedAuditValue>.Empty
                .Add("audience", RedactedAuditValue.FromComponentId(enforcement.Audience))
                .Add("effect", RedactedAuditValue.FromEffect(enforcement.Effect))
                .Add("fingerprint", RedactedAuditValue.FromFingerprint(new ContentHash(enforcement.InputFingerprint.Value)))
                .Add("kind", RedactedAuditValue.FromOperationKind(enforcement.Kind))
                .Add("resource", RedactedAuditValue.FromFingerprint(
                    DurableJournalSecurityBinding.FingerprintResource(enforcement.Resources[0]))),
            auditedAt);
    }
}
