// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Narrows an <see cref="IOutputPublisher"/> that additionally exposes a local, in-process live subscription.</summary>
/// <remarks>
/// <para>
/// This is an optional capability, not a widening of <see cref="IOutputPublisher"/> itself. Publishing events and
/// exposing the final envelope are universal requirements for every run; a local, in-process live subscription is
/// not. A purely durable or forwarding publisher — one that only appends to an external queue for a remote reader,
/// for example — has no in-process hub to subscribe to and legitimately implements only <see cref="IOutputPublisher"/>.
/// </para>
/// <para>
/// The facade selects the dependency-injection-resolved <see cref="IOutputPublisher"/> for a run and pattern-matches
/// it against this interface only when a caller requests a streaming run. A configured publisher that does not
/// implement this interface makes streaming an unsupported capability for that agent's composition; it is never a
/// silent fallback to polling or to a fabricated empty stream.
/// </para>
/// </remarks>
public interface ISubscribableOutputPublisher: IOutputPublisher
{
    /// <summary>Registers one bounded live subscriber whose final-result wait is independent of event delivery.</summary>
    /// <typeparam name="TOutput">The run's validated output snapshot type.</typeparam>
    /// <returns>A caller-owned stream that receives only subsequent publications and the single final envelope.</returns>
    /// <remarks>The first subscription or completion binds this run's output type. Abandoning the returned stream does not cancel the run.</remarks>
    /// <exception cref="InvalidOperationException">This run is already bound to a different output type.</exception>
    public IAgentRunStream<TOutput> Subscribe<TOutput>();
}
