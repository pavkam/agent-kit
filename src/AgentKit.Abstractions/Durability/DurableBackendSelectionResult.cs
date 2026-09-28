// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>The closed outcome of one durable backend selection attempt.</summary>
public abstract record DurableBackendSelectionResult
{
    /// <summary>Initializes a backend selection result.</summary>
    private protected DurableBackendSelectionResult()
    {
    }
}
