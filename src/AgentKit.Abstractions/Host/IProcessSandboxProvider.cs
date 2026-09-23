// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Builds one fail-closed platform launch for a named immutable sandbox profile.</summary>
public interface IProcessSandboxProvider
{
    /// <summary>Gets immutable metadata describing the enforced sandbox profile.</summary>
    public SandboxDescriptor Descriptor { get; }

    /// <summary>Prepares a sandbox wrapper without starting the protected process.</summary>
    /// <param name="request">The canonical sandbox construction input.</param>
    /// <param name="cancellationToken">Cancels preparation before process creation.</param>
    /// <returns>The enforcing wrapper launch or typed unsupported result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ProcessSandboxResult> CreateAsync(
        ProcessSandboxRequest request,
        CancellationToken cancellationToken = default);
}
