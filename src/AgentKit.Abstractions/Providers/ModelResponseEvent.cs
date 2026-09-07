// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The immutable base for one ordered event in the stream produced by an
/// <see cref="ILlmModel"/> attempt, delivered to an
/// <see cref="IModelResponseObserver"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a closed discriminated hierarchy. The concrete kinds are
/// <see cref="ModelResponseStarted"/>, <see cref="ModelPartStarted"/>,
/// <see cref="ModelPartDelta"/>, <see cref="ModelPartCompleted"/>,
/// <see cref="ModelUsageUpdated"/>, <see cref="ModelResponseCompleted"/>,
/// <see cref="ModelResponseFailed"/>, and
/// <see cref="ModelResponseCancelled"/>. Its constructor is
/// <see langword="private protected"/>, so no assembly outside
/// AgentKit.Abstractions can add a ninth kind.
/// </para>
/// <para>
/// Every attempt emits exactly one <see cref="ModelResponseStarted"/> first,
/// then zero or more <see cref="ModelPartStarted"/>,
/// <see cref="ModelPartDelta"/>, <see cref="ModelPartCompleted"/>, and
/// <see cref="ModelUsageUpdated"/> events, followed by exactly one of
/// <see cref="ModelResponseCompleted"/>, <see cref="ModelResponseFailed"/>,
/// or <see cref="ModelResponseCancelled"/>. <see cref="Sequence"/> values
/// for one <see cref="RequestId"/> are contiguous and strictly increasing;
/// no event follows a terminal event.
/// </para>
/// </remarks>
public abstract record ModelResponseEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModelResponseEvent"/>
    /// record. This constructor is <see langword="private protected"/> so
    /// only the closed set of kinds declared in this assembly can extend
    /// the hierarchy.
    /// </summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    private protected ModelResponseEvent(ModelRequestId requestId, long sequence)
    {
        RequestId = requestId;
        Sequence = sequence;
    }

    /// <summary>Gets the request this event belongs to.</summary>
    public ModelRequestId RequestId { get; init; }

    /// <summary>
    /// Gets the strictly increasing sequence number of this event within
    /// its request.
    /// </summary>
    public long Sequence { get; init; }
}
