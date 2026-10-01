// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Sends one message from one agent to another agent's session through the recipient's input path.</summary>
/// <remarks>
/// A channel owns translation from a message to admitted input and nothing else: it never authorizes on the sender's behalf beyond
/// the identity it is handed, never runs the recipient, and never bypasses admission, ordering, capacity, or idempotency.
/// Implementations are thread-safe, and a message is delivered at most once per idempotency key.
/// </remarks>
public interface IAgentMessageChannel
{
    /// <summary>Admits one message into the recipient session.</summary>
    /// <param name="request">The complete message.</param>
    /// <param name="cancellationToken">Cancels the wait before admission commits.</param>
    /// <returns>The durable admission, or a typed rejection; a refusal to admit is never an exception.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> is cancelled before admission commits.</exception>
    public ValueTask<AgentMessageResult> SendAsync(AgentMessageRequest request, CancellationToken cancellationToken = default);
}
