// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A normalized, portable description of one failed provider attempt,
/// retaining safe provider status, code, request identity, and retry hints
/// alongside the normalized <see cref="Kind"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// <see cref="SafeMessage"/> is deliberately distinct from
/// <see cref="DiagnosticCause"/>: the former is safe to surface to
/// end users or include in a model-visible tool error, while the latter may
/// carry sensitive transport detail and is intended for logs and
/// diagnostics only.
/// </para>
/// </remarks>
public sealed record ProviderFailure
{
    /// <summary>Initializes a new instance of the <see cref="ProviderFailure"/> record.</summary>
    /// <param name="kind">The normalized failure category.</param>
    /// <param name="providerId">The provider that produced or was targeted by the failed attempt.</param>
    /// <param name="requestId">The provider-supplied request correlation identifier, when available.</param>
    /// <param name="statusCode">The provider's transport-level status code, when applicable.</param>
    /// <param name="providerCode">The provider's own error code or type string, when available.</param>
    /// <param name="retryAfter">
    /// The provider-suggested minimum delay before retrying, when the
    /// provider supplied one.
    /// </param>
    /// <param name="safeMessage">
    /// A safe, non-sensitive description of the failure suitable for
    /// end-user or model-visible surfaces.
    /// </param>
    /// <param name="diagnosticCause">
    /// The underlying exception, when one exists, retained for logs and
    /// diagnostics only.
    /// </param>
    /// <param name="extensions">Provider-specific failure data.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of
    /// whitespace.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="extensions"/> is null.</exception>
    public ProviderFailure(
        ProviderFailureKind kind,
        ProviderId providerId,
        ProviderRequestId? requestId,
        int? statusCode,
        string? providerCode,
        TimeSpan? retryAfter,
        string safeMessage,
        Exception? diagnosticCause,
        ExtensionData extensions)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(extensions);

        Kind = kind;
        ProviderId = providerId;
        RequestId = requestId;
        StatusCode = statusCode;
        ProviderCode = providerCode;
        RetryAfter = retryAfter;
        SafeMessage = safeMessage;
        DiagnosticCause = diagnosticCause;
        Extensions = extensions;
    }

    /// <summary>Gets the normalized failure category.</summary>
    public ProviderFailureKind Kind { get; init; }

    /// <summary>Gets the provider that produced or was targeted by the failed attempt.</summary>
    public ProviderId ProviderId { get; init; }

    /// <summary>
    /// Gets the provider-supplied request correlation identifier, when
    /// available.
    /// </summary>
    public ProviderRequestId? RequestId { get; init; }

    /// <summary>Gets the provider's transport-level status code, when applicable.</summary>
    public int? StatusCode { get; init; }

    /// <summary>Gets the provider's own error code or type string, when available.</summary>
    public string? ProviderCode { get; init; }

    /// <summary>
    /// Gets the provider-suggested minimum delay before retrying, when the
    /// provider supplied one.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    /// <summary>
    /// Gets a safe, non-sensitive description of the failure suitable for
    /// end-user or model-visible surfaces.
    /// </summary>
    public string SafeMessage { get; init; }

    /// <summary>
    /// Gets the underlying exception, when one exists, retained for logs
    /// and diagnostics only.
    /// </summary>
    public Exception? DiagnosticCause { get; init; }

    /// <summary>Gets provider-specific failure data.</summary>
    public ExtensionData Extensions { get; init; }
}
