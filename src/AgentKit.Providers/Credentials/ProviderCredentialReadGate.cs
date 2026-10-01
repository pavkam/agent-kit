// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Credentials;

/// <summary>
/// Validates and consumes a credential-read grant for a credential source, so the source reads no secret before an exact,
/// single-use, audited authorization.
/// </summary>
/// <remarks>
/// <para>
/// A source calls <see cref="ConsumeAsync"/> first and reads secret material only when it returns
/// <see langword="null"/>. The gate checks, in order: the credential profile names this source; the grant's audience,
/// kind, effect, resource, fingerprint, and authorization context equal what the request in hand implies; and the grant
/// store atomically consumes one use with a matching enforcement-intent receipt and required audit. Any mismatch, store
/// refusal, replay, or unauditable consumption returns a typed
/// <see cref="ProviderFailureKind.Authorization"/> failure with a fixed safe message.
/// </para>
/// <para>
/// Instances are thread-safe, hold no per-request state, and are intended as engine-wide singletons registered by
/// <c>AddAgentProviders</c>. Custom credential sources reuse this gate so they inherit the same enforcement.
/// </para>
/// </remarks>
public sealed class ProviderCredentialReadGate
{
    private readonly ISecurityGrantStore _grantStore;
    private readonly ISecurityAuditDispatcher _auditDispatcher;
    private readonly IIdentifierGenerator<SecurityEnforcementIntentId> _intentIds;
    private readonly IIdentifierGenerator<SecurityAuditRecordId> _auditRecordIds;
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a credential-read gate over one explicit set of security collaborators.</summary>
    /// <param name="grantStore">The authoritative store that atomically consumes the credential-read grant.</param>
    /// <param name="auditDispatcher">The required audit dispatcher grant consumption must reach before a secret is read.</param>
    /// <param name="intentIds">The replaceable enforcement-intent identity source.</param>
    /// <param name="auditRecordIds">The replaceable audit-record identity source.</param>
    /// <param name="timeProvider">The clock that stamps audit records.</param>
    /// <exception cref="ArgumentNullException">A dependency is null.</exception>
    public ProviderCredentialReadGate(
        ISecurityGrantStore grantStore,
        ISecurityAuditDispatcher auditDispatcher,
        IIdentifierGenerator<SecurityEnforcementIntentId> intentIds,
        IIdentifierGenerator<SecurityAuditRecordId> auditRecordIds,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(grantStore);
        ArgumentNullException.ThrowIfNull(auditDispatcher);
        ArgumentNullException.ThrowIfNull(intentIds);
        ArgumentNullException.ThrowIfNull(auditRecordIds);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _grantStore = grantStore;
        _auditDispatcher = auditDispatcher;
        _intentIds = intentIds;
        _auditRecordIds = auditRecordIds;
        _timeProvider = timeProvider;
    }

    /// <summary>Gets the audience first-party credential sources enforce credential-read grants under.</summary>
    /// <value>A stable component identity distinct from the provider-egress, resolver, and transport audiences.</value>
    public static ComponentId DefaultAudience { get; } = new("agentkit.providers.credentials");

    /// <summary>Validates the request's grant against <paramref name="source"/> and consumes it once with required audit.</summary>
    /// <param name="source">The source that is about to read a secret.</param>
    /// <param name="request">The secret-free evidence and grant.</param>
    /// <param name="cancellationToken">A token used to cancel consumption.</param>
    /// <returns><see langword="null"/> when the source may read its secret; otherwise the typed refusal to return unchanged.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public async ValueTask<ProviderCredentialUnavailable?> ConsumeAsync(
        IProviderCredentialSource source,
        ProviderCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(request);

        var providerId = request.Credential.ProviderId;
        if (request.Credential.SourceKey != source.Key)
        {
            return Refuse(
                providerId,
                ProviderFailureKind.InvalidRequest,
                "The credential profile names a different credential source.");
        }

        var grant = request.CredentialGrant;
        var expectedResources = ProviderCredentialReadBinding.Resources(request.Credential);
        var expectedFingerprint = ProviderCredentialReadBinding.Fingerprint(
            request.Endpoint,
            request.Credential,
            request.Attempt,
            request.Deadline);
        if (grant.Audience != source.SecurityAudience
            || grant.Kind != ProviderCredentialReadBinding.OperationKind
            || grant.Effect != ProviderCredentialReadBinding.Effect
            || grant.Authorization != request.Operation.Authorization
            || !grant.Resources.SequenceEqual(expectedResources)
            || grant.InputFingerprint != expectedFingerprint)
        {
            return Refuse(
                providerId,
                ProviderFailureKind.Authorization,
                "The credential-read grant did not match this credential request.");
        }

        var enforcement = ProviderCredentialReadBinding.Enforcement(grant);
        var intent = new SecurityEnforcementIntent(_intentIds.Create(), null);
        string? denial;
        try
        {
            denial = await SecurityGrantConsumptionHostOperations.ConsumeWithRequiredAuditAsync(
                grant,
                enforcement,
                intent,
                _grantStore,
                _auditDispatcher,
                _auditRecordIds,
                _timeProvider,
                ProviderCredentialReadBinding.IsFreshExact,
                ProviderCredentialReadBinding.DenialMessage,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Refuse(
                providerId,
                ProviderFailureKind.Authorization,
                "Credential-read enforcement was unavailable, so the grant was not consumed.",
                exception);
        }

        return denial is null
            ? null
            : Refuse(providerId, ProviderFailureKind.Authorization, "The credential-read grant was refused at consumption.");
    }

    private static ProviderCredentialUnavailable Refuse(
        ProviderId providerId,
        ProviderFailureKind kind,
        string safeMessage,
        Exception? cause = null) =>
        new(new ProviderFailure(
            kind,
            providerId,
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            cause,
            ExtensionData.Empty));
}
