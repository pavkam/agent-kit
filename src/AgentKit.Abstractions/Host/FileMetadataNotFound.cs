// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The target did not exist at observation time.</summary>
public sealed record FileMetadataNotFound: FileMetadataResult
{
    /// <summary>Initializes a not-found metadata outcome.</summary>
    /// <param name="target">The resolved target that was missing.</param>
    public FileMetadataNotFound(ResolvedFileTarget target) => Target = target;

    /// <summary>Gets the resolved target that was missing.</summary>
    public ResolvedFileTarget Target { get; init; }
}
