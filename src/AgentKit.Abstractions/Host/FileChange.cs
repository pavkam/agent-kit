// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One change event observed during an authorized watch stream.</summary>
public sealed record FileChange
{
    /// <summary>Initializes one observed change.</summary>
    /// <param name="relativePath">The path of the affected entry relative to the watch root.</param>
    /// <param name="kind">The kind of change observed.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="relativePath"/> is default or <paramref name="kind"/> is undefined.</exception>
    public FileChange(NormalizedRelativePath relativePath, FileChangeKind kind)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(relativePath, default);
        ArgumentOutOfRangeException.ThrowIfUndefined(kind);
        RelativePath = relativePath;
        Kind = kind;
    }

    /// <summary>Gets the path of the affected entry relative to the watch root.</summary>
    public NormalizedRelativePath RelativePath { get; init; }

    /// <summary>Gets the kind of change observed.</summary>
    public FileChangeKind Kind { get; init; }
}
