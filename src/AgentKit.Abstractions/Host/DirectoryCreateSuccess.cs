// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Reports that the directory was created.</summary>
public sealed record DirectoryCreateSuccess: DirectoryCreateResult
{
    /// <summary>Initializes a successful directory creation outcome.</summary>
    /// <param name="target">The resolved directory target that now exists.</param>
    public DirectoryCreateSuccess(ResolvedFileTarget target) => Target = target;

    /// <summary>Gets the resolved directory target that now exists.</summary>
    public ResolvedFileTarget Target { get; init; }
}
