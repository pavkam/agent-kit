// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The terminal event of one successful attempt, carrying the committed
/// <see cref="Response"/>. No further events follow for the same request.
/// </summary>
public sealed record ModelResponseCompleted: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelResponseCompleted"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    /// <param name="response">The committed response.</param>
    /// <exception cref="ArgumentNullException"><paramref name="response"/> is null.</exception>
    public ModelResponseCompleted(ModelRequestId requestId, long sequence, ModelResponse response)
        : base(requestId, sequence)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
    }

    /// <summary>Gets the committed response.</summary>
    public ModelResponse Response { get; init; }
}
