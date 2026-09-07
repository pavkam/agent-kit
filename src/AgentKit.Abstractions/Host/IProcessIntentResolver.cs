// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Resolves configured executables and workspace paths into canonical pre-authorization facts.</summary>
public interface IProcessIntentResolver
{
    /// <summary>Resolves and fingerprints one structured process request without starting a process.</summary>
    /// <param name="request">The untrusted structured request.</param>
    /// <param name="cancellationToken">Cancels resolution before a process exists.</param>
    /// <returns>The resolved immutable intent or typed safe failure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="request"/> is null.</exception>
    public ValueTask<ProcessResolutionResult> ResolveAsync(
        ProcessResolveRequest request,
        CancellationToken cancellationToken = default);
}
