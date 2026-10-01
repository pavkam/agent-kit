// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the regular file was removed.</summary>
public sealed record FileDeleteSuccess: FileDeleteResult
{
    /// <summary>Initializes a successful deletion outcome.</summary>
    /// <param name="target">The resolved target that no longer exists.</param>
    /// <param name="previousBytes">The non-negative length of the removed file.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="previousBytes"/> is negative.</exception>
    public FileDeleteSuccess(ResolvedFileTarget target, long previousBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(previousBytes);
        Target = target;
        PreviousBytes = previousBytes;
    }

    /// <summary>Gets the resolved target that no longer exists.</summary>
    public ResolvedFileTarget Target { get; init; }

    /// <summary>Gets the length of the removed file in bytes.</summary>
    public long PreviousBytes { get; init; }
}
