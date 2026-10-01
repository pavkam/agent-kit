// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one <see cref="IToolResultSpill"/> request.</summary>
/// <remarks>The concrete kinds are <see cref="ToolResultSpilled"/> and <see cref="ToolResultNotSpilled"/>. The constructor is <see langword="private protected"/>, so no assembly outside AgentKit.Abstractions can extend the hierarchy.</remarks>
public abstract record ToolResultSpillResult
{
    private protected ToolResultSpillResult()
    {
    }
}
