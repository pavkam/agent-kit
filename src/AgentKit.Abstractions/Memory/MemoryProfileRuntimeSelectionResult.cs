// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Is the abstract outcome of activating a memory profile runtime: an owned lease, or a typed unavailable result.</summary>
public abstract record MemoryProfileRuntimeSelectionResult
{
    /// <summary>Restricts derivation to the contracts package.</summary>
    private protected MemoryProfileRuntimeSelectionResult()
    {
    }
}
