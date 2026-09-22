// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The write was cancelled before it reached a terminal committed outcome.</summary>
public sealed record FileWriteCancelled: FileWriteResult
{
    /// <summary>Initializes a new instance of the <see cref="FileWriteCancelled"/> record.</summary>
    /// <param name="sideEffectCertainty">Evidence about whether any host mutation occurred.</param>
    public FileWriteCancelled(SideEffectCertainty sideEffectCertainty) =>
        SideEffectCertainty = sideEffectCertainty;

    /// <summary>Gets evidence about whether any host mutation occurred.</summary>
    public SideEffectCertainty SideEffectCertainty { get; init; }
}
