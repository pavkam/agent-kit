// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Permissions.InMemory;

/// <summary>Provides a process-local, concurrency-safe security grant store for standalone hosts and deterministic tests.</summary>
/// <remarks>
/// Grant evidence and remaining-use state are retained for the lifetime of this singleton service. The store is not durable
/// across process loss; hosts requiring recovery must replace it through dependency injection with a durable implementation
/// that preserves the same atomic consumption contract.
/// </remarks>
public sealed class InMemorySecurityGrantStore: ISecurityGrantStore
{
    private readonly ConcurrentDictionary<GrantId, GrantState> _grants = new();
    private readonly Dictionary<SecurityEnforcementIntentId, SecurityEnforcementIntentReceipt> _intentReceipts = [];
    private readonly Lock _intentSyncRoot = new();
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<InMemorySecurityGrantStore> _logger;

    /// <summary>Initializes a process-local grant store without an application logger.</summary>
    /// <param name="timeProvider">The deterministic clock used for validity windows and receipts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public InMemorySecurityGrantStore(TimeProvider timeProvider)
        : this(timeProvider, null)
    {
    }

    /// <summary>Initializes a process-local grant store with safe structured diagnostics.</summary>
    /// <param name="timeProvider">The deterministic clock used for validity windows and receipts.</param>
    /// <param name="logger">The optional logger; a null logger is used when omitted.</param>
    /// <exception cref="ArgumentNullException"><paramref name="timeProvider"/> is null.</exception>
    public InMemorySecurityGrantStore(
        TimeProvider timeProvider,
        ILogger<InMemorySecurityGrantStore>? logger)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        _timeProvider = timeProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemorySecurityGrantStore>.Instance;
    }

    /// <inheritdoc/>
    public ValueTask RegisterAsync(SecurityGrant grant, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ValidateGrant(grant);
        cancellationToken.ThrowIfCancellationRequested();

        var state = new GrantState(grant);
        var registered = _grants.GetOrAdd(grant.Id, state);
        return GrantMatches(registered.Grant, grant)
            ? ValueTask.CompletedTask
            : throw new InvalidOperationException(
                $"Grant identifier '{grant.Id}' is already registered with different evidence.");
    }

    /// <inheritdoc/>
    public ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ValidateGrant(grant);
        ArgumentNullException.ThrowIfNull(enforcement.Scope);
        ArgumentNullException.ThrowIfNull(enforcement.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(enforcement.Resources);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_grants.TryGetValue(grant.Id, out var state))
        {
            return ValueTask.FromResult(Result(GrantConsumptionStatus.Unknown, 0, "The security grant is unknown."));
        }

        lock (state.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!GrantMatches(state.Grant, grant))
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Tampered, state.RemainingUses, "The security grant evidence does not match its authoritative record."));
            }

            if (state.Revoked || enforcement.RevocationVersion != grant.RevocationVersion)
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Revoked, state.RemainingUses, "The security grant is revoked or stale."));
            }

            var now = _timeProvider.GetUtcNow();
            if (now < grant.NotBefore || now >= grant.ExpiresAt)
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Expired, state.RemainingUses, "The security grant is outside its validity window."));
            }

            if (!EnforcementMatches(grant, enforcement))
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Mismatch, state.RemainingUses, "The concrete effect does not match the security grant."));
            }

            if (state.RemainingUses == 0)
            {
                return ValueTask.FromResult(Result(GrantConsumptionStatus.Exhausted, 0, "The security grant has no remaining uses."));
            }

            state.RemainingUses--;
            return ValueTask.FromResult(Result(GrantConsumptionStatus.Consumed, state.RemainingUses, "The security grant was consumed."));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<GrantConsumptionResult> ValidateAndConsumeAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        ValidateGrant(grant);
        ArgumentNullException.ThrowIfNull(enforcement.Scope);
        ArgumentNullException.ThrowIfNull(enforcement.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(enforcement.Resources);
        cancellationToken.ThrowIfCancellationRequested();

        using var activityScope = AgentKitActivityScope.Start(
            AgentKitActivityNames.SecurityGrantConsume,
            ActivityKind.Internal,
            new ActivityTagsCollection
            {
                { AgentKitTagNames.GenAiOperationName, AgentKitActivityNames.SecurityGrantConsume },
                { AgentKitTagNames.SecurityRequestId, grant.RequestId.ToString() },
                { AgentKitTagNames.AgentId, grant.Scope.AgentId.ToString() },
                { AgentKitTagNames.SessionId, grant.Scope.SessionId?.ToString() },
                { AgentKitTagNames.OperationId, grant.Scope.Correlation.OperationId.ToString() },
            });
        try
        {
            var result = await ValidateAndConsumeIntentCoreAsync(
                grant, enforcement, intent, cancellationToken).ConfigureAwait(false);
            var outcome = result.Status.ToString().ToLowerInvariant();
            SafeSetActivity(() =>
            {
                if (result.Status is GrantConsumptionStatus.Consumed or GrantConsumptionStatus.Reconciled)
                {
                    activityScope.Activity.SetSuccessful(outcome);
                }
                else
                {
                    activityScope.Activity.SetFailed(outcome, outcome);
                }
            });
            SafeLog(() => InMemorySecurityGrantStoreLog.GrantConsumptionCompleted(_logger, grant.RequestId, outcome));
            SafeMetric(outcome);
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SafeSetActivity(() => activityScope.Activity.SetFailed("cancelled", nameof(OperationCanceledException)));
            SafeLog(() => InMemorySecurityGrantStoreLog.GrantConsumptionCancelled(_logger, grant.RequestId));
            SafeMetric("cancelled");
            throw;
        }
        catch (Exception exception)
        {
            SafeSetActivity(() => activityScope.Activity.SetFailed("faulted", exception.GetType().Name));
            SafeLog(() => InMemorySecurityGrantStoreLog.GrantConsumptionFaulted(
                _logger, grant.RequestId, exception.GetType().Name));
            SafeMetric("faulted");
            throw;
        }
    }

    private ValueTask<GrantConsumptionResult> ValidateAndConsumeIntentCoreAsync(
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentNullException.ThrowIfNull(enforcement);
        ArgumentNullException.ThrowIfNull(intent);
        ValidateGrant(grant);
        ArgumentNullException.ThrowIfNull(enforcement.Scope);
        ArgumentNullException.ThrowIfNull(enforcement.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(enforcement.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(enforcement.Resources);
        cancellationToken.ThrowIfCancellationRequested();

        if (!_grants.TryGetValue(grant.Id, out var state))
        {
            return ValueTask.FromResult(Result(GrantConsumptionStatus.Unknown, 0, "The security grant is unknown."));
        }

        lock (_intentSyncRoot)
        {
            lock (state.SyncRoot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!GrantMatches(state.Grant, grant))
                {
                    return ValueTask.FromResult(Result(GrantConsumptionStatus.Tampered, state.RemainingUses,
                        "The security grant evidence does not match its authoritative record."));
                }

                var effectFingerprint = SecurityEnforcementBinding.Fingerprint(enforcement, intent);
                if (_intentReceipts.TryGetValue(intent.Id, out var existing))
                {
                    return ValueTask.FromResult(ReceiptMatches(existing, grant, enforcement, intent, effectFingerprint)
                        ? Result(GrantConsumptionStatus.Reconciled, state.RemainingUses,
                            "The enforcement intent receipt was reconciled without granting another effect.", existing)
                        : Result(GrantConsumptionStatus.Mismatch, state.RemainingUses,
                            "The enforcement intent identity was reused with different evidence."));
                }

                if (state.Revoked || enforcement.RevocationVersion != grant.RevocationVersion)
                {
                    return ValueTask.FromResult(Result(GrantConsumptionStatus.Revoked, state.RemainingUses,
                        "The security grant is revoked or stale."));
                }

                var now = _timeProvider.GetUtcNow();
                cancellationToken.ThrowIfCancellationRequested();
                if (now < grant.NotBefore || now >= grant.ExpiresAt)
                {
                    return ValueTask.FromResult(Result(GrantConsumptionStatus.Expired, state.RemainingUses,
                        "The security grant is outside its validity window."));
                }

                if (!EnforcementMatches(grant, enforcement))
                {
                    return ValueTask.FromResult(Result(GrantConsumptionStatus.Mismatch, state.RemainingUses,
                        "The concrete effect does not match the security grant."));
                }

                if (state.RemainingUses == 0)
                {
                    return ValueTask.FromResult(Result(GrantConsumptionStatus.Exhausted, 0,
                        "The security grant has no remaining uses."));
                }

                var remainingUses = state.RemainingUses - 1;
                var receipt = new SecurityEnforcementIntentReceipt(
                    intent.Id,
                    grant.Id,
                    grant.RequestId,
                    enforcement,
                    intent.RequiredFence,
                    effectFingerprint,
                    now);
                var consumed = Result(GrantConsumptionStatus.Consumed, remainingUses,
                    "The security grant and enforcement intent were consumed.", receipt);
                cancellationToken.ThrowIfCancellationRequested();
                _intentReceipts.Add(intent.Id, receipt);
                state.RemainingUses = remainingUses;
                return ValueTask.FromResult(consumed);
            }
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> RevokeAsync(GrantId grantId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_grants.TryGetValue(grantId, out var state))
        {
            return ValueTask.FromResult(false);
        }

        lock (state.SyncRoot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            state.Revoked = true;
            return ValueTask.FromResult(true);
        }
    }

    private static bool EnforcementMatches(SecurityGrant grant, SecurityEnforcementRequest enforcement)
    {
        Debug.Assert(grant is not null, "Registered grant evidence is required.");
        Debug.Assert(enforcement is not null, "Concrete enforcement evidence is required.");
        return grant.Scope == enforcement.Scope
            && grant.Identity == enforcement.Identity
            && grant.Authorization == enforcement.Authorization
            && grant.Audience == enforcement.Audience
            && grant.Kind == enforcement.Kind
            && grant.Effect == enforcement.Effect
            && grant.InputFingerprint == enforcement.InputFingerprint
            && grant.Resources.SequenceEqual(enforcement.Resources);
    }

    private static bool ReceiptMatches(
        SecurityEnforcementIntentReceipt receipt,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent,
        ContentHash effectFingerprint)
    {
        Debug.Assert(receipt is not null, "An authoritative intent receipt is required.");
        Debug.Assert(grant is not null, "Presented grant evidence is required.");
        Debug.Assert(enforcement is not null, "Presented enforcement evidence is required.");
        Debug.Assert(intent is not null, "Presented intent evidence is required.");
        return receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && EnforcementEvidenceMatches(receipt.Enforcement, enforcement)
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == effectFingerprint;
    }

    private static bool EnforcementEvidenceMatches(
        SecurityEnforcementRequest expected,
        SecurityEnforcementRequest actual)
    {
        Debug.Assert(expected is not null, "Authoritative enforcement evidence is required.");
        Debug.Assert(actual is not null, "Presented enforcement evidence is required.");
        return expected.Scope == actual.Scope
            && expected.Identity == actual.Identity
            && expected.Authorization == actual.Authorization
            && expected.Audience == actual.Audience
            && expected.Kind == actual.Kind
            && expected.Effect == actual.Effect
            && expected.Resources.SequenceEqual(actual.Resources)
            && expected.InputFingerprint == actual.InputFingerprint
            && expected.RevocationVersion == actual.RevocationVersion;
    }

    private static bool GrantMatches(SecurityGrant expected, SecurityGrant actual)
    {
        Debug.Assert(expected is not null, "Authoritative grant evidence is required.");
        Debug.Assert(actual is not null, "Presented grant evidence is required.");
        return expected.Id == actual.Id
            && expected.RequestId == actual.RequestId
            && expected.Scope == actual.Scope
            && expected.Identity == actual.Identity
            && expected.Authorization == actual.Authorization
            && expected.Audience == actual.Audience
            && expected.Kind == actual.Kind
            && expected.Effect == actual.Effect
            && expected.Resources.SequenceEqual(actual.Resources)
            && expected.InputFingerprint == actual.InputFingerprint
            && expected.PolicyVersion == actual.PolicyVersion
            && expected.RevocationVersion == actual.RevocationVersion
            && expected.NotBefore == actual.NotBefore
            && expected.ExpiresAt == actual.ExpiresAt
            && expected.AllowedUses == actual.AllowedUses;
    }

    private static GrantConsumptionResult Result(GrantConsumptionStatus status, int remainingUses, string message,
        SecurityEnforcementIntentReceipt? receipt = null)
    {
        Debug.Assert(Enum.IsDefined(status), "A defined grant-consumption status is required.");
        Debug.Assert(remainingUses >= 0, "Remaining uses cannot be negative.");
        Debug.Assert(!string.IsNullOrWhiteSpace(message), "A safe result message is required.");
        return new GrantConsumptionResult(status, remainingUses, message, receipt);
    }

    private static void ValidateGrant(SecurityGrant grant)
    {
        Debug.Assert(grant is not null, "A caller-validated grant is required.");
        ArgumentNullException.ThrowIfNull(grant.Scope);
        ArgumentNullException.ThrowIfNull(grant.Identity);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Kind);
        ArgumentOutOfRangeException.ThrowIfUndefined(grant.Effect);
        ArgumentException.ThrowIfDefaultOrEmpty(grant.Resources);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(grant.ExpiresAt, grant.NotBefore);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(grant.AllowedUses);
    }

    private static void SafeSetActivity(Action action)
    {
        Debug.Assert(action is not null, "Activity observation requires a callback.");
        try
        {
            action();
        }
        catch
        {
            // Activity listeners are observational and cannot alter grant consumption.
        }
    }

    private static void SafeLog(Action action)
    {
        Debug.Assert(action is not null, "Log observation requires a callback.");
        try
        {
            action();
        }
        catch
        {
            // Logging providers are observational and cannot alter grant consumption.
        }
    }

    private static void SafeMetric(string outcome)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(outcome), "A bounded terminal outcome is required.");
        try
        {
            InMemorySecurityGrantStoreMetrics.GrantConsumptions.Add(
                1,
                new KeyValuePair<string, object?>(AgentKitTagNames.Outcome, outcome));
        }
        catch
        {
            // Meter listeners are observational and cannot alter grant consumption.
        }
    }

    private sealed class GrantState(SecurityGrant grant)
    {
        /// <summary>Gets the immutable registered grant evidence.</summary>
        /// <value>The authoritative grant retained under its stable identity.</value>
        public SecurityGrant Grant { get; } = grant;
        /// <summary>Gets the per-grant synchronization object.</summary>
        /// <value>The private monitor serializing mutable state for this grant.</value>
        public object SyncRoot { get; } = new();
        /// <summary>Gets or sets the positive or zero uses that remain.</summary>
        /// <value>The authoritative count changed only while holding <see cref="SyncRoot"/>.</value>
        public int RemainingUses { get; set; } = grant.AllowedUses;
        /// <summary>Gets or sets whether future fresh intents are revoked.</summary>
        /// <value><see langword="true"/> after idempotent revocation commits.</value>
        public bool Revoked { get; set; }
    }
}
