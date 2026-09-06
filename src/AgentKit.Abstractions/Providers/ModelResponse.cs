// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The committed, terminal result of one successful <see cref="IChatModel"/>
/// attempt, carried by <see cref="ModelResponseCompleted"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// <see cref="Parts"/> must equal the ordered set of parts carried by every
/// emitted <see cref="ModelPartCompleted"/> event for the same request, and
/// <see cref="Usage"/> must equal the last <see cref="ModelUsageUpdated"/>
/// value emitted (or the adapter's final usage report when no update event
/// was ever emitted). This record only ever represents success; failed or
/// cancelled attempts use <see cref="ModelResponseFailed"/> or
/// <see cref="ModelResponseCancelled"/> instead, which preserve bounded
/// partial parts without presenting them as a committed response.
/// </para>
/// </remarks>
public sealed record ModelResponse
{
    /// <summary>Initializes a new instance of the <see cref="ModelResponse"/> record.</summary>
    /// <param name="requestId">The request this response answers.</param>
    /// <param name="identity">The exact provider, model, and correlation identity that served the request.</param>
    /// <param name="parts">The ordered, complete content of the response.</param>
    /// <param name="stopReason">The normalized, portable stop reason.</param>
    /// <param name="usage">Token usage and cost accounting for this response.</param>
    /// <param name="extensions">Provider-specific response metadata.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="identity"/>, <paramref name="usage"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="parts"/> is a default, uninitialized array.
    /// </exception>
    public ModelResponse(
        ModelRequestId requestId,
        ProviderResponseIdentity identity,
        ImmutableArray<ContentPart> parts,
        NormalizedStopReason stopReason,
        ModelUsage usage,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfDefault(parts);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(extensions);

        RequestId = requestId;
        Identity = identity;
        Parts = parts;
        StopReason = stopReason;
        Usage = usage;
        Extensions = extensions;
    }

    /// <summary>Gets the request this response answers.</summary>
    public ModelRequestId RequestId { get; init; }

    /// <summary>Gets the exact provider, model, and correlation identity that served the request.</summary>
    public ProviderResponseIdentity Identity { get; init; }

    /// <summary>Gets the ordered, complete content of the response.</summary>
    public ImmutableArray<ContentPart> Parts { get; init; }

    /// <summary>Gets the normalized, portable stop reason.</summary>
    public NormalizedStopReason StopReason { get; init; }

    /// <summary>Gets token usage and cost accounting for this response.</summary>
    public ModelUsage Usage { get; init; }

    /// <summary>Gets provider-specific response metadata.</summary>
    public ExtensionData Extensions { get; init; }
}
