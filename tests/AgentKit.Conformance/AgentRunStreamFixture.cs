// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Provides deterministic producer controls for reusable non-owning stream conformance.</summary>
/// <remarks>Adapters bind the coordinates from RunResultTestData and retain at least two events. Disposal releases producer and subscriber resources without live services.</remarks>
public abstract class AgentRunStreamFixture: IAsyncDisposable
{
    /// <summary>Gets the already registered stream under test.</summary><value>The same nonnull subscription for this fixture.</value>
    public abstract IAgentRunStream<string> Stream { get; }
    /// <summary>Accepts a deterministic event before returning.</summary><param name="runEvent">The correlated immutable event.</param><param name="cancellationToken">Cancels the fixture publication wait.</param><returns>Completion of acceptance into the stream's buffer.</returns>
    public abstract ValueTask PublishAsync(RunEvent runEvent, CancellationToken cancellationToken);
    /// <summary>Finishes event production and exposes the exact final envelope.</summary><param name="result">The correlated final result.</param><param name="cancellationToken">Cancels the fixture completion wait.</param><returns>Completion of final-result exposure.</returns>
    public abstract ValueTask CompleteAsync(AgentRunFinished<string> result, CancellationToken cancellationToken);
    /// <summary>Releases all resources created by this fixture.</summary><returns>Completion of fixture cleanup.</returns>
    public abstract ValueTask DisposeAsync();
}
