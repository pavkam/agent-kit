// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The first event of every attempt, signaling that the provider has
/// accepted the request and a response is beginning.
/// </summary>
public sealed record ModelResponseStarted: ModelResponseEvent
{
    /// <summary>Initializes a new instance of the <see cref="ModelResponseStarted"/> record.</summary>
    /// <param name="requestId">The request this event belongs to.</param>
    /// <param name="sequence">The strictly increasing sequence number of this event within its request.</param>
    public ModelResponseStarted(ModelRequestId requestId, long sequence)
        : base(requestId, sequence)
    {
    }
}
