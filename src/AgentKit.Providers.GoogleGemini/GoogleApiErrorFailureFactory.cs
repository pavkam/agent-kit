// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers.GoogleGemini;

using System.Net.Http;

using AgentKit.Providers.GoogleGemini.Wire;
using AgentKit.Providers.Http;

/// <summary>
/// Builds the normalized <see cref="ProviderFailure"/> for a non-success
/// HTTP response from any Google API surface that returns a
/// <c>google.rpc.Status</c> error envelope, so the Gemini Developer API and
/// Google Cloud Vertex AI adapters classify failures identically.
/// </summary>
/// <remarks>
/// <para>
/// Classification order is fixed: the body's canonical <c>error.status</c>
/// is mapped through <see cref="GoogleGeminiErrorMapping"/> first, and only
/// when that yields <see cref="ProviderFailureKind.Unknown"/> (the status is
/// absent, the body is missing or malformed, or Google introduced a status
/// this table does not know) does the shared HTTP status table in
/// <see cref="HttpStatusFailureKindMapper"/> decide. The raw HTTP status,
/// the canonical status string as <see cref="ProviderFailure.ProviderCode"/>,
/// and any <c>Retry-After</c> guidance are always retained.
/// </para>
/// <para>
/// The body's <c>error.message</c> is untrusted provider prose. It is never
/// placed in <see cref="ProviderFailure.SafeMessage"/>, which is a fixed
/// status template; it is retained only as bounded diagnostic evidence via
/// <see cref="ProviderErrorMessageEvidence"/>. A body that cannot be read
/// or parsed does not fail the classification: the exception is kept as
/// <see cref="ProviderFailure.DiagnosticCause"/> and the HTTP status remains
/// authoritative.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently.
/// </para>
/// </remarks>
public static class GoogleApiErrorFailureFactory
{
    /// <summary>
    /// Reads the error body of <paramref name="response"/> and builds the
    /// normalized failure for it.
    /// </summary>
    /// <param name="response">
    /// The non-success response whose headers have been received. Its
    /// content is read once by this method; the caller retains ownership
    /// and disposes the response.
    /// </param>
    /// <param name="providerId">The provider identity to record on the failure.</param>
    /// <param name="timeProvider">The clock used to resolve an absolute <c>Retry-After</c> date.</param>
    /// <param name="cancellationToken">
    /// Cancels reading the error body. Cancellation propagates as
    /// <see cref="OperationCanceledException"/>; it is deliberately not
    /// folded into the failure so callers can distinguish caller
    /// cancellation, deadline expiry, and transport timeout themselves.
    /// </param>
    /// <returns>
    /// The normalized failure. Its <see cref="ProviderFailure.StatusCode"/>
    /// is always the HTTP status; <see cref="ProviderFailure.ProviderCode"/>
    /// is the canonical status string when the body carried one.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled while the body was being read.</exception>
    public static async Task<ProviderFailure> CreateAsync(
        HttpResponseMessage response,
        ProviderId providerId,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(timeProvider);

        string? providerMessage = null;
        string? status = null;
        Exception? diagnosticCause = null;

        try
        {
            var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using (body.ConfigureAwait(false))
            {
                var envelope = await JsonSerializer
                    .DeserializeAsync<GoogleGeminiErrorEnvelopeDto>(body, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
                providerMessage = envelope?.Error?.Message;
                status = envelope?.Error?.Status;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The error body was malformed, truncated, or the connection failed while reading it. The HTTP status
            // is still authoritative evidence, so fall back to a status-only failure and keep the cause for diagnostics.
            diagnosticCause = exception;
        }

        var mappedBodyKind = GoogleGeminiErrorMapping.MapStatus(status);
        var kind = mappedBodyKind == ProviderFailureKind.Unknown
            ? HttpStatusFailureKindMapper.Map(response.StatusCode)
            : mappedBodyKind;

        return new ProviderFailure(
            kind,
            providerId,
            requestId: null,
            (int) response.StatusCode,
            status,
            RetryAfterResolver.Resolve(response.Headers, timeProvider),
            $"The provider returned HTTP status {(int) response.StatusCode}.",
            diagnosticCause,
            ProviderErrorMessageEvidence.Create(providerMessage));
    }

    /// <summary>
    /// Builds the failure for an error response whose body read was
    /// interrupted by cancellation, deadline expiry, or a transport timeout,
    /// preserving the response evidence already received.
    /// </summary>
    /// <param name="response">The response whose headers were received before the interruption.</param>
    /// <param name="providerId">The provider identity to record on the failure.</param>
    /// <param name="kind">
    /// The interruption classification the caller established, normally
    /// <see cref="ProviderFailureKind.Cancellation"/> or
    /// <see cref="ProviderFailureKind.Timeout"/>.
    /// </param>
    /// <param name="safeMessage">The bounded message safe for ordinary application handling.</param>
    /// <param name="diagnosticCause">The classified exception that interrupted the body read, when any.</param>
    /// <param name="timeProvider">The clock used to resolve an absolute <c>Retry-After</c> date.</param>
    /// <returns>
    /// A failure of <paramref name="kind"/> that retains the raw HTTP
    /// status and <c>Retry-After</c> guidance but no provider code or
    /// message evidence, since the body was never fully read.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> or <paramref name="timeProvider"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> is not a defined <see cref="ProviderFailureKind"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public static ProviderFailure CreateInterrupted(
        HttpResponseMessage response,
        ProviderId providerId,
        ProviderFailureKind kind,
        string safeMessage,
        Exception? diagnosticCause,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ArgumentNullException.ThrowIfNull(timeProvider);

        return new ProviderFailure(
            kind,
            providerId,
            requestId: null,
            (int) response.StatusCode,
            providerCode: null,
            RetryAfterResolver.Resolve(response.Headers, timeProvider),
            safeMessage,
            diagnosticCause,
            ExtensionData.Empty);
    }
}
