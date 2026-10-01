// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Releases one disposable <see cref="IProviderCredentialLease"/> for a provider credential profile, but only after it
/// validates and consumes the credential-read grant the caller obtained from the security authority.
/// </summary>
/// <remarks>
/// <para>
/// A credential source is the enforcing component of the credential-read effect. The caller (normally
/// <c>ProviderEgress</c>) asks the security authority selected by the operation's captured authorization context for a
/// grant whose audience is <see cref="SecurityAudience"/>, whose kind is <see cref="SecurityOperationKind.StateRead"/>,
/// whose effect is <see cref="SecurityEffect.Observe"/>, and whose resource and fingerprint bind the exact credential
/// profile revision, source key, account, attempt, and deadline. The source validates that the grant matches the
/// request in hand, consumes it exactly once with required audit, and only then reads secret material. A denied,
/// mismatched, expired, replayed, or unauditable grant yields <see cref="ProviderCredentialUnavailable"/> before any
/// secret is read. A provider-egress or network grant can never authorize a credential read, and a credential-read grant
/// can never authorize network send.
/// </para>
/// <para>
/// Sources are registered under their <see cref="ProviderCredentialSourceKey"/> and are selected only through a
/// captured credential profile; no unkeyed source is ever resolved. Implementations must be safe to call concurrently
/// from multiple in-flight attempts, must resolve fresh for each call so a rotated key or refreshed token takes effect
/// on the next attempt, and must never log, serialize, or place secret material in an exception message.
/// </para>
/// </remarks>
public interface IProviderCredentialSource
{
    /// <summary>Gets the stable key the credential profile uses to select this source.</summary>
    /// <value>The non-default source key this instance is registered under.</value>
    public ProviderCredentialSourceKey Key { get; }

    /// <summary>Gets the exact enforcement audience that consumes credential-read grants for this source.</summary>
    /// <value>A stable component identity distinct from the provider-egress, resolver, and transport audiences.</value>
    public ComponentId SecurityAudience { get; }

    /// <summary>Validates and consumes the credential-read grant, then releases one credential lease.</summary>
    /// <param name="request">The secret-free profile snapshots, operation, attempt, deadline, and grant.</param>
    /// <param name="cancellationToken">A token used to cancel grant consumption or secret resolution.</param>
    /// <returns>
    /// A <see cref="ProviderCredentialResolved"/> owning one disposable lease, or a
    /// <see cref="ProviderCredentialUnavailable"/> carrying a normalized failure. No secret is read when the result is
    /// <see cref="ProviderCredentialUnavailable"/> because of grant validation.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
        ProviderCredentialResolutionRequest request,
        CancellationToken cancellationToken = default);
}
