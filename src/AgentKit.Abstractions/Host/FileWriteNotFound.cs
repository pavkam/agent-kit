// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The target did not exist for a disposition that requires an existing file.</summary>
public sealed record FileWriteNotFound: FileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteNotFound"/> record.</summary>
    /// <param name="target">The resolved target that was missing.</param>
    public FileWriteNotFound(ResolvedFileTarget target) => Target = target;

    /// <summary>Gets the resolved target that was missing.</summary>
    public ResolvedFileTarget Target { get; init; }
}
