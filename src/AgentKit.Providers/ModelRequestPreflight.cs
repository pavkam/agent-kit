// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Providers;

/// <summary>
/// Performs the provider-neutral request preflight every first-party model
/// adapter runs before any credential resolution, translation, or I/O:
/// descriptor identity and the capability assertions a request makes
/// against the adapter's configured descriptor.
/// </summary>
/// <remarks>
/// <para>
/// An adapter instance serves exactly one descriptor. The request context
/// carries the descriptor the selector chose, and the two must be
/// structurally equal: a mismatched identity would let the adapter send one
/// model identifier on the wire while reporting another in the response
/// identity, and mismatched capabilities would let translation rely on
/// features the adapter never verified. The identity check therefore runs
/// first and uses record equality over the whole descriptor.
/// </para>
/// <para>
/// The remaining checks are capability assertions the request makes
/// explicitly: exposing tools requires <see cref="ModelCapabilities.SupportsToolCalls"/>,
/// and requesting <see cref="LlmRequestSettings.ParallelToolCalls"/> with
/// tools present requires <see cref="ModelCapabilities.SupportsParallelToolCalls"/>.
/// A request that leaves <see cref="LlmRequestSettings.ParallelToolCalls"/>
/// unset or <see langword="false"/> asserts nothing and passes.
/// </para>
/// <para>
/// Every rejection is a <see cref="ProviderFailureKind.InvalidRequest"/>
/// failure attributed to the descriptor's provider with no request
/// identity, status, provider code, or retry hint, because no provider
/// contact occurred. The safe messages are fixed templates shared by every
/// adapter so callers can rely on one wording per rejection.
/// </para>
/// <para>
/// This type is stateless and safe to call concurrently. It never throws for
/// a rejected request; it throws only when a caller passes a null argument.
/// </para>
/// </remarks>
public static class ModelRequestPreflight
{
    /// <summary>The safe message reported when the request descriptor is not the adapter's descriptor.</summary>
    private const string _descriptorMismatchMessage = "The request model descriptor does not match the configured adapter descriptor.";

    /// <summary>The safe message reported when tools are requested from a model that cannot call them.</summary>
    private const string _toolCallsUnsupportedMessage = "The selected model does not support tool calls.";

    /// <summary>The safe message reported when parallel tool calls are requested from a model that cannot issue them.</summary>
    private const string _parallelToolCallsUnsupportedMessage = "The selected model does not support parallel tool calls.";

    /// <summary>
    /// Validates one conversational request against the adapter's configured
    /// descriptor, checking descriptor identity, tool support, and parallel
    /// tool-call support in that order.
    /// </summary>
    /// <param name="request">The request the adapter was asked to execute.</param>
    /// <param name="descriptor">The descriptor the adapter instance was constructed to serve.</param>
    /// <returns>
    /// <see langword="null"/> when the request may proceed to credential
    /// resolution and translation; otherwise an
    /// <see cref="ProviderFailureKind.InvalidRequest"/> failure describing
    /// the first violated precondition.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public static ProviderFailure? Validate(LlmModelRequest request, ModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(descriptor);

        var capabilities = descriptor.Capabilities;
        return request.Context switch
        {
            { Model: var model } when model != descriptor =>
                Reject(descriptor.ProviderId, _descriptorMismatchMessage),
            { Tools.Length: 0 } => null,
            _ when !capabilities.SupportsToolCalls =>
                Reject(descriptor.ProviderId, _toolCallsUnsupportedMessage),
            { Settings.ParallelToolCalls: true } when !capabilities.SupportsParallelToolCalls =>
                Reject(descriptor.ProviderId, _parallelToolCallsUnsupportedMessage),
            _ => null,
        };
    }

    /// <summary>
    /// Validates one embedding request against the adapter's configured
    /// descriptor, checking descriptor identity.
    /// </summary>
    /// <param name="request">The request the adapter was asked to execute.</param>
    /// <param name="descriptor">The descriptor the adapter instance was constructed to serve.</param>
    /// <returns>
    /// <see langword="null"/> when the request may proceed to credential
    /// resolution and translation; otherwise an
    /// <see cref="ProviderFailureKind.InvalidRequest"/> failure describing
    /// the descriptor mismatch.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> or <paramref name="descriptor"/> is <see langword="null"/>.</exception>
    public static ProviderFailure? Validate(EmbeddingModelRequest request, EmbeddingModelDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(descriptor);

        return request.Context.Model != descriptor
            ? Reject(descriptor.ProviderId, _descriptorMismatchMessage)
            : null;
    }

    private static ProviderFailure Reject(ProviderId providerId, string safeMessage)
    {
        Debug.Assert(!string.IsNullOrWhiteSpace(safeMessage), "Every preflight rejection must carry a fixed, non-blank safe message.");

        return new ProviderFailure(
            ProviderFailureKind.InvalidRequest,
            providerId,
            requestId: null,
            statusCode: null,
            providerCode: null,
            retryAfter: null,
            safeMessage,
            diagnosticCause: null,
            ExtensionData.Empty);
    }
}
