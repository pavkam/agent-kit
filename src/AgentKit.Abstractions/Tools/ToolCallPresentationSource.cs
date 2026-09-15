// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Wraps the original unvalidated model-authored call projection for preview or approval rendering.</summary>
public sealed record ToolCallPresentationSource: ToolPresentationSource
{
    /// <summary>Initializes the source.</summary><param name="call">The nonnull immutable call projection.</param><exception cref="ArgumentNullException"><paramref name="call"/> is null.</exception>
    public ToolCallPresentationSource(ToolCallPart call) { ArgumentNullException.ThrowIfNull(call); Call = call; }
    /// <summary>Gets the original call projection.</summary><value>A nonnull immutable value.</value>
    public ToolCallPart Call { get; }
}
