// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Credentials;

using AgentKit.Providers.Http;

/// <summary>
/// The first-party <see cref="IProviderCredentialLease"/> over an opaque <see cref="ProviderCredential"/> (an API key or an
/// OAuth access token), applied as the HTTP header the target's <see cref="ProviderAuthorizationScheme"/> declares.
/// </summary>
/// <remarks>
/// <para>
/// The lease holds the credential privately and exposes no accessor for it. <see cref="ApplyAsync"/> maps it through
/// <see cref="ProviderAuthorizationHeaderFactory"/>: an API key uses the scheme's header name and prefix (or is refused
/// when the scheme has no API-key mode), and an OAuth token is sent as a bearer token unless expired. Refusals carry
/// fixed safe messages and never contain credential material. <see cref="ToString"/> prints only the type name.
/// </para>
/// <para>
/// <see cref="DisposeAsync"/> drops the lease's reference to the credential so it cannot be applied again. The managed
/// string values inside <see cref="ApiKeyProviderCredential"/> and <see cref="OAuthTokenProviderCredential"/> are
/// immutable and cannot be zeroed; the lease limits their reachability rather than overwriting them.
/// </para>
/// <para>
/// Disposal and application are safe to race: application captures the credential once, and disposal wins at most by
/// causing a later application to throw.
/// </para>
/// </remarks>
public sealed class ProviderCredentialLease: IProviderCredentialLease
{
    private ProviderCredential? _credential;

    /// <summary>Initializes a lease over <paramref name="credential"/>.</summary>
    /// <param name="credential">The opaque credential the lease will apply.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is null.</exception>
    public ProviderCredentialLease(ProviderCredential credential)
    {
        ArgumentNullException.ThrowIfNull(credential);

        _credential = credential;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The lease has been disposed.</exception>
    public ValueTask<ProviderFailure?> ApplyAsync(
        IProviderAuthenticationTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var credential = Volatile.Read(ref _credential) ?? throw new ObjectDisposedException(nameof(ProviderCredentialLease));
        var authorization = ProviderAuthorizationHeaderFactory.Create(
            credential,
            target.ProviderId,
            target.UtcNow,
            target.Scheme);
        switch (authorization)
        {
            case ProviderAuthorizationDenied denied:
                return ValueTask.FromResult<ProviderFailure?>(denied.Failure);
            case ProviderAuthorizationGranted granted:
                target.SetHeader(granted.HeaderName, granted.HeaderValue);
                return ValueTask.FromResult<ProviderFailure?>(null);
            default:
                throw new InvalidOperationException("The authorization factory returned an unsupported result.");
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Exchange(ref _credential, null);
        return ValueTask.CompletedTask;
    }

    /// <summary>Returns the type name only; the credential is never rendered.</summary>
    /// <returns>The fixed text <c>ProviderCredentialLease</c>.</returns>
    public override string ToString() => nameof(ProviderCredentialLease);
}
