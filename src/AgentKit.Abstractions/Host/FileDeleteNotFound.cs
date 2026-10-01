// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The target did not exist, so nothing was removed.</summary>
public sealed record FileDeleteNotFound: FileDeleteResult
{
    /// <summary>Initializes a not-found deletion outcome.</summary>
    /// <param name="target">The resolved target that was missing.</param>
    public FileDeleteNotFound(ResolvedFileTarget target) => Target = target;

    /// <summary>Gets the resolved target that was missing.</summary>
    public ResolvedFileTarget Target { get; init; }
}
