// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Credentials;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> that asks an application-supplied <see cref="IOAuthAccessTokenProvider"/>
/// for the current OAuth bearer token only after validating and consuming the credential-read grant.
/// </summary>
/// <remarks>
/// The token provider is never called for a denied, mismatched, expired, or replayed grant. A provider that throws, or
/// that violates its non-nullable contract by returning <see langword="null"/>, produces a typed
/// <see cref="ProviderFailureKind.Authentication"/> failure with a fixed safe message; the provider's exception is
/// retained only as diagnostic cause. <see cref="ToString"/> prints only the type name and source key.
/// </remarks>
public sealed class DelegatingOAuthCredentialSource: IProviderCredentialSource
{
    private readonly IOAuthAccessTokenProvider _tokenProvider;
    private readonly ProviderCredentialReadGate _gate;

    /// <summary>Initializes a delegating OAuth source.</summary>
    /// <param name="key">The source key the credential profile selects this source by.</param>
    /// <param name="tokenProvider">Supplies the application's current OAuth access token.</param>
    /// <param name="gate">The gate that validates and consumes the credential-read grant.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is the default value.</exception>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    public DelegatingOAuthCredentialSource(
        ProviderCredentialSourceKey key,
        IOAuthAccessTokenProvider tokenProvider,
        ProviderCredentialReadGate gate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(gate);

        Key = key;
        _tokenProvider = tokenProvider;
        _gate = gate;
    }

    /// <inheritdoc/>
    public ProviderCredentialSourceKey Key { get; }

    /// <inheritdoc/>
    public ComponentId SecurityAudience => ProviderCredentialReadGate.DefaultAudience;

    /// <inheritdoc/>
    public async ValueTask<ProviderCredentialResolutionResult> ResolveAsync(
        ProviderCredentialResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (await _gate.ConsumeAsync(this, request, cancellationToken).ConfigureAwait(false) is { } refusal)
        {
            return refusal;
        }

        OAuthTokenProviderCredential? token;
        try
        {
            token = await _tokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(request, "The OAuth access token could not be resolved.", exception);
        }

        return token is null
            ? Unavailable(request, "The OAuth access token provider returned no token.", cause: null)
            : new ProviderCredentialResolved(new ProviderCredentialLease(token));
    }

    /// <summary>Returns the type name and source key only; no token is rendered.</summary>
    /// <returns>A redacted textual form.</returns>
    public override string ToString() => $"{nameof(DelegatingOAuthCredentialSource)} {{ Key = {Key.Value} }}";

    private static ProviderCredentialUnavailable Unavailable(
        ProviderCredentialResolutionRequest request,
        string safeMessage,
        Exception? cause) =>
        new(new ProviderFailure(
            ProviderFailureKind.Authentication,
            request.Credential.ProviderId,
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            cause,
            ExtensionData.Empty));
}
