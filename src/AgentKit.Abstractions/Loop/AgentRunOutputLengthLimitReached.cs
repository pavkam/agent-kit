// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The run stopped because the model's terminal response reported
/// <see cref="NormalizedStopReason.Length"/>: an output or token ceiling was
/// reached before the model finished, so the response is truthful partial
/// output rather than a chosen completion.
/// </summary>
/// <remarks>
/// <para>
/// This type is an immutable value object with structural equality over its
/// fields. It carries no mutable state and is safe to share across threads
/// without synchronization.
/// </para>
/// <para>
/// This is a model-attempt limit observed by the loop, distinct from the
/// budget-authority limit reported through <see cref="RunLimitReached"/>: the
/// loop has no budget scope, dimension, or reservation evidence for a provider
/// length stop and does not fabricate one. When the attempt produced partial
/// parts, the loop commits them as an <see cref="MessageState.Interrupted"/>
/// assistant message before settling with this outcome and reports that
/// through <see cref="HasPartialOutput"/>.
/// </para>
/// </remarks>
public sealed record AgentRunOutputLengthLimitReached: AgentRunOutcome
{
    /// <summary>Initializes a new instance of the <see cref="AgentRunOutputLengthLimitReached"/> record.</summary>
    /// <param name="modelRequestId">The non-default identity of the model request whose output was truncated.</param>
    /// <param name="hasPartialOutput">Whether truncated partial output was committed as an interrupted message.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation that contains no model output.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="modelRequestId"/> is the default identity.</exception>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or consists only of whitespace.</exception>
    public AgentRunOutputLengthLimitReached(ModelRequestId modelRequestId, bool hasPartialOutput, string safeMessage)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(modelRequestId, default);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        ModelRequestId = modelRequestId;
        HasPartialOutput = hasPartialOutput;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the identity of the model request whose output was truncated.</summary>
    /// <value>A non-default identity correlating this outcome to the attempt, its stream events, and any committed interrupted message.</value>
    public ModelRequestId ModelRequestId { get; }

    /// <summary>Gets whether truncated partial output was committed as an interrupted assistant message.</summary>
    /// <value><see langword="true"/> when the result's committed messages include the truncated output; otherwise, <see langword="false"/>.</value>
    public bool HasPartialOutput { get; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    /// <value>Bounded diagnostic text that never includes model output.</value>
    public string SafeMessage { get; }
}
