// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

using AgentKit.Providers.Credentials;

/// <summary>
/// The Bedrock <see cref="IProviderCredentialSource"/>: it validates and consumes the credential-read grant, and only
/// then asks the application's <see cref="IAwsCredentialSource"/> for the current AWS credential and releases an
/// <see cref="AwsSigV4CredentialLease"/> that signs the request.
/// </summary>
/// <remarks>
/// The application-supplied <see cref="IAwsCredentialSource"/> is never called for a denied, mismatched, expired, or
/// replayed grant. A supplier that throws yields a typed <see cref="ProviderFailureKind.Authentication"/> failure with a
/// fixed safe message; the exception is retained only as diagnostic cause. <see cref="ToString"/> prints only the type
/// name and source key.
/// </remarks>
public sealed class AwsSigV4CredentialSource: IProviderCredentialSource
{
    private readonly IAwsCredentialSource _supplier;
    private readonly string _region;
    private readonly string _service;
    private readonly ProviderCredentialReadGate _gate;

    /// <summary>Initializes a Bedrock credential source.</summary>
    /// <param name="key">The source key the credential profile selects this source by.</param>
    /// <param name="supplier">Supplies the application's current AWS credential.</param>
    /// <param name="region">The non-empty AWS region requests are signed for.</param>
    /// <param name="service">The non-empty AWS signing service name.</param>
    /// <param name="gate">The gate that validates and consumes the credential-read grant.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="key"/> is the default value.</exception>
    /// <exception cref="ArgumentNullException">A reference argument is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="region"/> or <paramref name="service"/> is null, empty, or whitespace.</exception>
    public AwsSigV4CredentialSource(
        ProviderCredentialSourceKey key,
        IAwsCredentialSource supplier,
        string region,
        string service,
        ProviderCredentialReadGate gate)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(key, default);
        ArgumentNullException.ThrowIfNull(supplier);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);
        ArgumentNullException.ThrowIfNull(gate);

        Key = key;
        _supplier = supplier;
        _region = region;
        _service = service;
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

        AwsSigV4Credential? credential;
        try
        {
            credential = await _supplier.GetCredentialAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Unavailable(request, "The AWS credential could not be resolved.", exception);
        }

        return credential is null
            ? Unavailable(request, "The AWS credential source returned no credential.", cause: null)
            : new ProviderCredentialResolved(new AwsSigV4CredentialLease(credential, _region, _service));
    }

    /// <summary>Returns the type name and source key only; no credential is rendered.</summary>
    /// <returns>A redacted textual form.</returns>
    public override string ToString() => $"{nameof(AwsSigV4CredentialSource)} {{ Key = {Key.Value} }}";

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
