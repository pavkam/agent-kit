// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Credentials;

using System.Security.Cryptography;
using System.Text.Json;

/// <summary>Creates the exact, secret-free resource and fingerprint a credential-read grant binds.</summary>
/// <remarks>
/// <para>
/// The requester (normally <c>ProviderEgress</c>) and the enforcing credential source compute the same values from the
/// same evidence, so a grant issued for one profile revision, source, account, attempt, or deadline can never release
/// a lease for another. The fingerprint binds the provider, service surface, endpoint profile reference and
/// configuration fingerprint, credential profile reference and configuration fingerprint, source key, account,
/// attempt, and deadline. The grant itself carries the execution identity and authorization context.
/// </para>
/// <para>
/// Nothing here reads, contains, or derives from secret material.
/// </para>
/// </remarks>
public static class ProviderCredentialReadBinding
{
    /// <summary>The operation kind every credential-read grant carries.</summary>
    public const SecurityOperationKind OperationKind = SecurityOperationKind.StateRead;

    /// <summary>The effect every credential-read grant carries.</summary>
    public const SecurityEffect Effect = SecurityEffect.Observe;

    /// <summary>Creates the single protected resource a credential-read grant names.</summary>
    /// <param name="credential">The secret-free credential profile snapshot.</param>
    /// <returns>An application-state resource identifying the profile revision and source, never the secret.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is null.</exception>
    public static ImmutableArray<ProtectedResource> Resources(ProviderCredentialProfileSnapshot credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        return
        [
            new ProtectedResource(
                ProtectedResourceKind.ApplicationState,
                $"provider-credential:{credential.ProviderId.Value}/{credential.ServiceSurface.Value}/"
                + $"{credential.Reference.Key.Value}@{credential.Reference.Version.Value}/source:{credential.SourceKey.Value}"),
        ];
    }

    /// <summary>Computes the exact credential-read fingerprint without retaining secrets.</summary>
    /// <param name="endpoint">The selected endpoint profile snapshot.</param>
    /// <param name="credential">The selected credential profile snapshot.</param>
    /// <param name="attempt">The one-based attempt number.</param>
    /// <param name="deadline">The absolute attempt deadline.</param>
    /// <returns>An algorithm-qualified SHA-256 fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="endpoint"/> or <paramref name="credential"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="attempt"/> is less than one.</exception>
    public static InputFingerprint Fingerprint(
        ProviderEndpointProfileSnapshot endpoint,
        ProviderCredentialProfileSnapshot credential,
        int attempt,
        DateTimeOffset deadline)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);

        var payload = new
        {
            operation = "provider-credential-read-v1",
            provider = credential.ProviderId.Value,
            serviceSurface = credential.ServiceSurface.Value,
            endpointProfile = new
            {
                key = endpoint.Reference.Key.Value,
                version = endpoint.Reference.Version.Value,
                fingerprint = endpoint.ConfigurationFingerprint.Value,
            },
            credentialProfile = new
            {
                key = credential.Reference.Key.Value,
                version = credential.Reference.Version.Value,
                fingerprint = credential.ConfigurationFingerprint.Value,
            },
            source = credential.SourceKey.Value,
            account = credential.AccountId?.Value,
            attempt,
            deadlineTicks = deadline.UtcTicks,
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        return new InputFingerprint($"sha256:{Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()}");
    }

    /// <summary>Creates the exact enforcement evidence a source presents when it consumes its grant.</summary>
    /// <param name="grant">The credential-read grant issued for the attempt.</param>
    /// <returns>Enforcement evidence naming the grant's own audience, resources, and fingerprint.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant"/> is null.</exception>
    internal static SecurityEnforcementRequest Enforcement(SecurityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new SecurityEnforcementRequest(
            grant.Scope,
            grant.Identity,
            grant.Authorization,
            grant.Audience,
            grant.Kind,
            grant.Effect,
            grant.Resources,
            grant.InputFingerprint,
            grant.RevocationVersion);
    }

    /// <summary>Determines whether a consumption result is a new exact use of the presented grant.</summary>
    /// <param name="consumption">The atomic grant-store result.</param>
    /// <param name="grant">The grant presented to the store.</param>
    /// <param name="enforcement">The evidence presented to the store.</param>
    /// <param name="intent">The freshly generated enforcement intent.</param>
    /// <returns><see langword="true"/> only for a newly consumed use with a complete matching receipt.</returns>
    internal static bool IsFreshExact(
        GrantConsumptionResult consumption,
        SecurityGrant grant,
        SecurityEnforcementRequest enforcement,
        SecurityEnforcementIntent intent)
    {
        Debug.Assert(consumption is not null, "The grant store always returns a result.");
        Debug.Assert(grant is not null, "The caller presents the grant it consumed.");
        Debug.Assert(enforcement is not null, "The caller presents the evidence it consumed.");
        Debug.Assert(intent is not null, "The caller presents the fresh intent it consumed.");

        return consumption.Status is GrantConsumptionStatus.Consumed
            && consumption.IntentReceipt is { } receipt
            && receipt.IntentId == intent.Id
            && receipt.GrantId == grant.Id
            && receipt.RequestId == grant.RequestId
            && receipt.RequiredFence == intent.RequiredFence
            && receipt.EffectFingerprint == SecurityEnforcementBinding.Fingerprint(enforcement, intent)
            && receipt.Enforcement.Scope == enforcement.Scope
            && receipt.Enforcement.Identity == enforcement.Identity
            && receipt.Enforcement.Authorization == enforcement.Authorization
            && receipt.Enforcement.Audience == enforcement.Audience
            && receipt.Enforcement.Kind == enforcement.Kind
            && receipt.Enforcement.Effect == enforcement.Effect
            && receipt.Enforcement.InputFingerprint == enforcement.InputFingerprint
            && receipt.Enforcement.RevocationVersion == enforcement.RevocationVersion
            && receipt.Enforcement.Resources.SequenceEqual(enforcement.Resources);
    }

    /// <summary>Returns a stable non-content denial message for a consumption that failed fresh exact validation.</summary>
    /// <param name="consumption">The failed consumption result.</param>
    /// <returns>The store message, or a fixed message when a consumed result lacks its receipt.</returns>
    internal static string DenialMessage(GrantConsumptionResult consumption)
    {
        Debug.Assert(consumption is not null, "The grant store always returns a result.");
        return consumption.Status is GrantConsumptionStatus.Consumed
            ? "The grant store did not retain a fresh exact enforcement-intent receipt."
            : consumption.SafeMessage;
    }
}
