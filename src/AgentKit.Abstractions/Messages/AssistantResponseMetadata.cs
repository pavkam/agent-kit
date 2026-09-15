// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// Provider, usage, and stop-reason metadata retained for one committed
/// <see cref="AssistantMessage"/>.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This record is the durable record of "what actually happened" for one
/// model response, kept separate from the response's visible
/// <see cref="AgentMessage.Parts"/> so that provenance, cost, and
/// correlation data survive independently of how the content itself is
/// displayed, repaired, or translated for a later provider request. Every
/// value here traces back to the exact <see cref="RequestId"/> that was
/// allocated before context preparation began, so usage accounting,
/// budgets, and support diagnostics can all reconstruct the same story from
/// this one record.
/// </para>
/// </remarks>
public sealed record AssistantResponseMetadata
{
    /// <summary>Initializes a new instance of the <see cref="AssistantResponseMetadata"/> record.</summary>
    /// <param name="requestId">
    /// The model request this response answers. The same identity flows
    /// through context preparation, provider attempts, and stream events.
    /// </param>
    /// <param name="response">The exact provider response identity.</param>
    /// <param name="stopReason">The normalized, portable stop reason.</param>
    /// <param name="rawStopReason">
    /// The provider's original, unnormalized stop reason text, preserved
    /// for diagnostics even though <paramref name="stopReason"/> is what
    /// the runtime acts on.
    /// </param>
    /// <param name="usage">Usage evidence for this response, retaining the provider report's lifecycle state.</param>
    /// <param name="extensions">Provider-specific response metadata.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="response"/>, <paramref name="usage"/>, or
    /// <paramref name="extensions"/> is null.
    /// </exception>
    public AssistantResponseMetadata(
        ModelRequestId requestId,
        ProviderResponseIdentity response,
        NormalizedStopReason stopReason,
        string? rawStopReason,
        ModelUsage usage,
        ExtensionData extensions)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(usage);
        ArgumentNullException.ThrowIfNull(extensions);

        RequestId = requestId;
        Response = response;
        StopReason = stopReason;
        RawStopReason = rawStopReason;
        Usage = usage;
        Extensions = extensions;
    }

    /// <summary>
    /// Gets the model request this response answers. The same identity
    /// flows through context preparation, provider attempts, and stream
    /// events.
    /// </summary>
    public ModelRequestId RequestId { get; init; }

    /// <summary>Gets the exact provider response identity.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ProviderResponseIdentity Response
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets the normalized, portable stop reason.</summary>
    public NormalizedStopReason StopReason { get; init; }

    /// <summary>
    /// Gets the provider's original, unnormalized stop reason text,
    /// preserved for diagnostics even though <see cref="StopReason"/> is
    /// what the runtime acts on.
    /// </summary>
    public string? RawStopReason { get; init; }

    /// <summary>Gets usage evidence for this response with its independent provider report state.</summary>
    /// <value>A non-null report preserving unknown fields as null and reported zero as a known value.</value>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ModelUsage Usage
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }

    /// <summary>Gets provider-specific response metadata.</summary>
    /// <exception cref="ArgumentNullException">
    /// The value assigned during initialization or non-destructive mutation is null.
    /// </exception>
    public ExtensionData Extensions
    {
        get;
        init
        {
            ArgumentNullException.ThrowIfNull(value);
            field = value;
        }
    }
}
