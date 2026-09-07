// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Runs one resolved non-interactive process while owning termination, reaping, and bounded output capture.</summary>
public interface IProcessRunner
{
    /// <summary>Gets the sole component audience permitted to consume process-run grants.</summary>
    public ComponentId SecurityAudience { get; }

    /// <summary>Revalidates authority and canonical facts, then runs and settles one process tree.</summary>
    /// <param name="request">The resolved intent and exact single-use authority.</param>
    /// <param name="cancellationToken">Cancels intent; after creation the runner terminates and settles the owned tree.</param>
    /// <returns>The bounded output tails and truthful terminal effect certainty.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ProcessRunResult> RunAsync(
        ProcessRunRequest request,
        CancellationToken cancellationToken = default);
}
