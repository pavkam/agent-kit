// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>
/// A logical file target bound to an explicit root and normalized relative
/// path.
/// </summary>
/// <remarks>
/// This type cannot represent an ambient current directory or an
/// unnormalized host path.
/// </remarks>
public sealed record FileTarget
{
    /// <summary>Initializes a new instance of the <see cref="FileTarget"/> record.</summary>
    /// <param name="rootId">The configured root that owns the target.</param>
    /// <param name="path">The normalized path relative to <paramref name="rootId"/>.</param>
    public FileTarget(FileRootId rootId, NormalizedRelativePath path)
    {
        RootId = rootId;
        Path = path;
    }

    /// <summary>Gets the configured root that owns the target.</summary>
    public FileRootId RootId { get; init; }

    /// <summary>Gets the normalized path relative to <see cref="RootId"/>.</summary>
    public NormalizedRelativePath Path { get; init; }
}
