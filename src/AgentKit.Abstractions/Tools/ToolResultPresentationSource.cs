// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Wraps a bounded history/model result projection without treating it as an authoritative terminal record.</summary>
public sealed record ToolResultPresentationSource: ToolPresentationSource
{
    /// <summary>Initializes the source.</summary><param name="result">The nonnull immutable result projection.</param><exception cref="ArgumentNullException"><paramref name="result"/> is null.</exception>
    public ToolResultPresentationSource(ToolResultPart result) { ArgumentNullException.ThrowIfNull(result); Result = result; }
    /// <summary>Gets the explicitly projected result evidence.</summary><value>A nonnull immutable projection.</value>
    public ToolResultPart Result { get; }
}
