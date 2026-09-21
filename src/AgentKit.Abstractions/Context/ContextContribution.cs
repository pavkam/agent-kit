// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The bounded outcome of one context contributor invocation.</summary>
public sealed record ContextContribution
{
    /// <summary>Initializes one contributor outcome.</summary>
    /// <param name="candidates">Zero or more proposed candidates in contributor-defined order.</param>
    /// <param name="diagnostics">Safe diagnostics emitted while building the contribution.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="candidates"/> or <paramref name="diagnostics"/> is a default, uninitialized array.
    /// </exception>
    public ContextContribution(
        ImmutableArray<ContextCandidate> candidates,
        ImmutableArray<ContextDiagnostic> diagnostics)
    {
        ArgumentException.ThrowIfDefault(candidates);
        ArgumentException.ThrowIfDefault(diagnostics);

        Candidates = candidates;
        Diagnostics = diagnostics;
    }

    /// <summary>Gets zero or more proposed candidates in contributor-defined order.</summary>
    public ImmutableArray<ContextCandidate> Candidates { get; }

    /// <summary>Gets safe diagnostics emitted while building the contribution.</summary>
    public ImmutableArray<ContextDiagnostic> Diagnostics { get; }
}
