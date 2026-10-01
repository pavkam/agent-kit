// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The target state did not match the authorized precondition, or the target is not a regular file, so nothing was removed.</summary>
public sealed record FileDeleteConflict: FileDeleteResult
{
    /// <summary>Initializes a conflicting deletion outcome.</summary>
    /// <param name="target">The resolved target whose state conflicted.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentException"><paramref name="safeMessage"/> is null, empty, or whitespace.</exception>
    public FileDeleteConflict(ResolvedFileTarget target, string safeMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        Target = target;
        SafeMessage = safeMessage;
    }

    /// <summary>Gets the resolved target whose state conflicted.</summary>
    public ResolvedFileTarget Target { get; init; }

    /// <summary>Gets a human-readable, non-sensitive explanation.</summary>
    public string SafeMessage { get; init; }
}
