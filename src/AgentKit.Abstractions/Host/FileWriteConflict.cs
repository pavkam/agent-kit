// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// The target state did not match the authorized precondition or disposition,
/// so no mutation occurred.
/// </summary>
public sealed record FileWriteConflict: FileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteConflict"/> record.</summary>
    /// <param name="target">The resolved target whose state conflicted.</param>
    /// <param name="safeMessage">A human-readable, non-sensitive explanation.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="safeMessage"/> is null, empty, or consists only of whitespace.
    /// </exception>
    public FileWriteConflict(ResolvedFileTarget target, string safeMessage)
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
