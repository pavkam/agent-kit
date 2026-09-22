// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The directory target already existed at creation time.</summary>
public sealed record DirectoryCreateAlreadyExists: DirectoryCreateResult
{
    /// <summary>Initializes an already-exists directory creation outcome.</summary>
    /// <param name="target">The resolved target that already existed.</param>
    public DirectoryCreateAlreadyExists(ResolvedFileTarget target) => Target = target;

    /// <summary>Gets the resolved target that already existed.</summary>
    public ResolvedFileTarget Target { get; init; }
}
