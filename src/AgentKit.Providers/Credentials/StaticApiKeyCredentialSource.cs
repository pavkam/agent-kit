// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.Credentials;

/// <summary>
/// An <see cref="IProviderCredentialSource"/> that releases one static, long-lived API key under a validated
/// credential-read grant, shared by any provider integration that authenticates with a plain API key.
/// </summary>
/// <remarks>
/// The application configures one API key (typically from a secret store). Each resolution first validates and consumes
/// the credential-read grant through <see cref="ProviderCredentialReadGate"/>, and only then returns a new
/// <see cref="ProviderCredentialLease"/> over the key. A denied or mismatched grant never reaches the key. An
/// application that rotates keys without restarting, or authenticates with a refreshable OAuth token, registers
/// <see cref="DelegatingOAuthCredentialSource"/> or its own source instead. <see cref="ToString"/> prints only the type
/// name and source key.
/// </remarks>
public sealed class StaticApiKeyCredentialSource: IProviderCredentialSource
{
    private readonly ApiKeyProviderCredential _credential;
    private readonly ProviderCredentialReadGate _gate;

    /// <summary>Initializes a static API-key source.</summary>
    /// <param name="key">The source key the credential profile selects this source by.</param>
    /// <param name="apiKey">The non-empty API key text.</param>
    /// <param name="gate">The gate that validates and consumes the credential-read grant.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is the default value.</exception>
    /// <exception cref="ArgumentException"><paramref name="apiKey"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="gate"/> is null.</exception>
    public StaticApiKeyCredentialSource(
        ProviderCredentialSourceKey key,
        string apiKey,
        ProviderCredentialReadGate gate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentNullException.ThrowIfNull(gate);

        Key = key;
        _credential = new ApiKeyProviderCredential(apiKey);
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

        return await _gate.ConsumeAsync(this, request, cancellationToken).ConfigureAwait(false) is { } refusal
            ? refusal
            : new ProviderCredentialResolved(new ProviderCredentialLease(_credential));
    }

    /// <summary>Returns the type name and source key only; the API key is never rendered.</summary>
    /// <returns>A redacted textual form.</returns>
    public override string ToString() => $"{nameof(StaticApiKeyCredentialSource)} {{ Key = {Key.Value} }}";
}
