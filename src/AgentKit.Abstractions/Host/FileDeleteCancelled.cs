// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Deletion was cancelled before it reached a terminal outcome.</summary>
public sealed record FileDeleteCancelled: FileDeleteResult
{
    /// <summary>Initializes a cancelled deletion outcome.</summary>
    /// <param name="sideEffectCertainty">Evidence about whether any host mutation occurred.</param>
    public FileDeleteCancelled(SideEffectCertainty sideEffectCertainty) => SideEffectCertainty = sideEffectCertainty;

    /// <summary>Gets evidence about whether any host mutation occurred.</summary>
    public SideEffectCertainty SideEffectCertainty { get; init; }
}
