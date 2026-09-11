// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Exposes one accepted run's incremental events and repeatedly awaitable final result.</summary>
/// <typeparam name="TOutput">The validated output snapshot type.</typeparam>
/// <remarks>Subscription disposal and enumeration cancellation release only local delivery resources. They never silently abort durable run work or cancel Completion; durable abort uses the explicit run-control surface.</remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "The normative public contract is a typed event stream, not a byte stream.")]
public interface IAgentRunStream<TOutput>: IAsyncDisposable
{
    /// <summary>Gets the accepted agent identity.</summary>
    /// <value>A nondefault identity shared by events and completion.</value>
    public AgentId AgentId { get; }
    /// <summary>Gets the owning session identity.</summary>
    /// <value>A nondefault identity shared by events and completion.</value>
    public SessionId SessionId { get; }
    /// <summary>Gets the optional conversation correlation.</summary>
    /// <value>A nondefault identity or null, matching events and completion.</value>
    public ConversationId? ConversationId { get; }
    /// <summary>Gets the accepted run identity.</summary>
    /// <value>A nondefault identity; rejected admission never creates this handle.</value>
    public RunId RunId { get; }
    /// <summary>Reads the bounded buffered prefix and subsequent events incrementally.</summary>
    /// <param name="cancellationToken">Cancels this subscription only, without altering durable work or Completion.</param>
    /// <returns>A single-consumer ordered event stream; explicit delivery failure never fabricates run completion.</returns>
    /// <exception cref="OperationCanceledException">The subscription token is cancelled.</exception>
    /// <exception cref="InvalidOperationException">The subscription is already being enumerated or cannot continue delivery.</exception>
    public IAsyncEnumerable<RunEvent> ReadAllAsync(CancellationToken cancellationToken = default);
    /// <summary>Gets the identical final envelope published after the bounded settlement attempt.</summary>
    /// <value>A repeatedly awaitable task independent of enumeration and subscription disposal.</value>
    public Task<AgentRunFinished<TOutput>> Completion { get; }
}
