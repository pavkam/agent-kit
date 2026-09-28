// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Durability.InMemory;

/// <summary>Grant-consuming, audited ingress for every protected journal operation.</summary>
public sealed partial class InMemoryDurableOperationJournal
{
    /// <summary>Gets the audience every journal grant must have been issued for.</summary>
    /// <value>The stable component identity of this adapter. A grant minted for another audience is refused.</value>
    public ComponentId SecurityAudience { get; } = new("agentkit.durability.in-memory");

    /// <summary>Recomputes enforcement evidence, consumes the single-use grant, and completes required audit.</summary>
    /// <typeparam name="TRequest">The immutable journal request shape.</typeparam>
    /// <param name="wrapper">The protected request carrying the grant and stable enforcement intent.</param>
    /// <param name="address">The operation coordinates the concrete effect targets.</param>
    /// <param name="authorization">The authorization capture the effect must run under.</param>
    /// <param name="requiredFence">
    /// The ownership generation the effect must present, or null for an unfenced authorized read.
    /// </param>
    /// <param name="fingerprint">The canonical digest over the complete request.</param>
    /// <param name="kind">The protected operation kind this method enforces.</param>
    /// <param name="effect">The protected effect this method enforces.</param>
    /// <param name="cancellationToken">Cancels before the grant is consumed or audit is dispatched.</param>
    /// <returns>
    /// A receipt when the grant was consumed and audit accepted, otherwise a content-free denial reason. Exactly
    /// one of the two is non-null.
    /// </returns>
    /// <remarks>
    /// The journal fails closed. A mismatched journal key, a fence the request does not actually carry, an
    /// authorization capture that cannot describe the address, an unconsumed or already-exhausted grant, invalid
    /// intent evidence, or unavailable required audit all deny before any record is written. Cancellation
    /// propagates; every other prerequisite failure becomes a denial rather than an exception, because an
    /// unavailable authority must never look like a committed write.
    /// </remarks>
    private async ValueTask<(DurableJournalEnforcementReceipt? Receipt, string? Denial)> EnforceAsync<TRequest>(
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

        if (wrapper.JournalKey != Key)
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
            SecurityAudience,
            kind,
            effect,
            [DurableJournalSecurityBinding.Resource(Key, address)],
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
    /// <returns>True only when agent, session, operation, run, and optional turn all match exactly.</returns>
    /// <remarks>
    /// This is the same rule <see cref="DurableOperationBinding"/> enforces, applied to an evidence read whose
    /// request carries only an address. It accepts in-run work and after-run follow-up whose address retains the
    /// causal run with no turn, and refuses sessionless authorization.
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
    /// <returns>True only when every identity, fence, and effect fingerprint matches exactly.</returns>
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
    /// <returns>True only when scope, identity, authorization, audience, kind, effect, digest, revocation, and resources match.</returns>
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
    /// The record fingerprints the protected resource rather than naming it, so an operation address never reaches
    /// an audit sink as readable text. No payload, state, or credential is included.
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
