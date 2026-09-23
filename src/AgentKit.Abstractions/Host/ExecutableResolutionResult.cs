// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The immutable base for executable resolution outcomes.</summary>
public abstract record ExecutableResolutionResult
{
    /// <summary>Initializes the base resolution outcome.</summary>
    private protected ExecutableResolutionResult()
    {
    }
}

/// <summary>Resolution produced canonical facts ready for authorization.</summary>
public sealed record ExecutableResolved: ExecutableResolutionResult
{
    /// <summary>Initializes a successful resolution.</summary>
    /// <param name="resolved">The resolved start facts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="resolved"/> is null.</exception>
    public ExecutableResolved(ResolvedProcessStart resolved)
    {
        ArgumentNullException.ThrowIfNull(resolved);
        Resolved = resolved;
    }

    /// <summary>Gets the resolved start facts.</summary>
    public ResolvedProcessStart Resolved { get; init; }
}

/// <summary>Resolution failed before canonical facts could be established.</summary>
/// <param name="SafeMessage">The non-sensitive failure explanation.</param>
public sealed record ExecutableResolutionFailed(string SafeMessage): ExecutableResolutionResult;

/// <summary>Resolution was denied by policy before protected observation.</summary>
/// <param name="SafeMessage">The non-sensitive denial explanation.</param>
public sealed record ExecutableResolutionDenied(string SafeMessage): ExecutableResolutionResult;
