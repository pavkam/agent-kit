// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.AwsBedrock;

/// <summary>
/// An <see cref="IProviderCredentialLease"/> over one <see cref="AwsSigV4Credential"/> that signs the prepared request
/// with AWS Signature Version 4 when applied.
/// </summary>
/// <remarks>
/// <para>
/// The lease signs exactly the method, address, content type, and frozen body bytes the
/// <see cref="IProviderAuthenticationTarget"/> describes, then sets the resulting <c>Authorization</c>,
/// <c>x-amz-date</c>, <c>x-amz-content-sha256</c>, and (for temporary credentials) <c>x-amz-security-token</c> headers.
/// It exposes no accessor for the credential, and <see cref="ToString"/> prints only the type name.
/// </para>
/// <para>
/// <see cref="DisposeAsync"/> releases the reference to the credential so the lease cannot sign again. The credential's
/// managed strings are immutable and cannot be zeroed.
/// </para>
/// </remarks>
public sealed class AwsSigV4CredentialLease: IProviderCredentialLease
{
    private readonly string _region;
    private readonly string _service;
    private AwsSigV4Credential? _credential;

    /// <summary>Initializes a lease that signs for one region and service.</summary>
    /// <param name="credential">The AWS credential the lease signs with.</param>
    /// <param name="region">The non-empty AWS region the request is scoped to.</param>
    /// <param name="service">The non-empty AWS signing service name.</param>
    /// <exception cref="ArgumentNullException"><paramref name="credential"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="region"/> or <paramref name="service"/> is null, empty, or whitespace.</exception>
    public AwsSigV4CredentialLease(AwsSigV4Credential credential, string region, string service)
    {
        ArgumentNullException.ThrowIfNull(credential);
        ArgumentException.ThrowIfNullOrWhiteSpace(region);
        ArgumentException.ThrowIfNullOrWhiteSpace(service);

        _credential = credential;
        _region = region;
        _service = service;
    }

    /// <inheritdoc/>
    /// <exception cref="ObjectDisposedException">The lease has been disposed.</exception>
    public ValueTask<ProviderFailure?> ApplyAsync(
        IProviderAuthenticationTarget target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        var credential = Volatile.Read(ref _credential) ?? throw new ObjectDisposedException(nameof(AwsSigV4CredentialLease));
        var signingHeaders = new Dictionary<string, string>(StringComparer.Ordinal);
        if (target.ContentType is { Length: > 0 } contentType)
        {
            signingHeaders["content-type"] = contentType;
        }

        var signed = AwsSigV4Signer.SignRequest(
            target.Method,
            target.RequestUri,
            signingHeaders,
            target.Body.Span,
            credential,
            _region,
            _service,
            target.UtcNow);
        foreach (var (name, value) in signed)
        {
            target.SetHeader(name, value);
        }

        return ValueTask.FromResult<ProviderFailure?>(null);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _ = Interlocked.Exchange(ref _credential, null);
        return ValueTask.CompletedTask;
    }

    /// <summary>Returns the type name only; the credential is never rendered.</summary>
    /// <returns>The fixed text <c>AwsSigV4CredentialLease</c>.</returns>
    public override string ToString() => nameof(AwsSigV4CredentialLease);
}
