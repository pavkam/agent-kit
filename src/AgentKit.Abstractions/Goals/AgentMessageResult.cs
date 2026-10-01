// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of outcomes from sending one agent-to-agent message.</summary>
/// <remarks>External assemblies cannot extend this hierarchy, so a consumer can exhaustively distinguish admission from rejection.</remarks>
public abstract record AgentMessageResult
{
    private protected AgentMessageResult()
    {
    }
}
