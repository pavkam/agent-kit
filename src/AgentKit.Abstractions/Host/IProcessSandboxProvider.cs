// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds one fail-closed platform launch for a named immutable sandbox profile.</summary>
public interface IProcessSandboxProvider
{
    /// <summary>Gets the exact profile identity this provider enforces.</summary>
    public SandboxProfileId ProfileId { get; }

    /// <summary>Prepares a sandbox wrapper without starting the protected process.</summary>
    /// <param name="intent">The canonical process intent to constrain.</param>
    /// <param name="cancellationToken">Cancels preparation before process creation.</param>
    /// <returns>The enforcing wrapper launch or typed unsupported result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="intent"/> is null.</exception>
    public ValueTask<ProcessSandboxResult> PrepareAsync(
        ResolvedProcessIntent intent,
        CancellationToken cancellationToken = default);
}
