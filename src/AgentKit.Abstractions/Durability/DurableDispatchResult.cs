// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one durable backend dispatch attempt.</summary>
public abstract record DurableDispatchResult
{
    /// <summary>Initializes a dispatch result.</summary>
    private protected DurableDispatchResult()
    {
    }
}
