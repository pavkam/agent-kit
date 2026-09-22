// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Requests termination of one owned process handle.</summary>
public sealed record ProcessTerminationRequest
{
    /// <summary>Initializes a termination request.</summary>
    /// <param name="reason">The non-sensitive termination reason.</param>
    /// <exception cref="ArgumentException"><paramref name="reason"/> is null, empty, or whitespace.</exception>
    public ProcessTerminationRequest(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    /// <summary>Gets the non-sensitive termination reason.</summary>
    public string Reason { get; init; }
}
