// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Describes the consistency guarantee a session store makes visible to its callers.</summary>
/// <remarks>Composition uses this declaration to reject profiles whose recovery or concurrency requirements exceed a store's documented guarantees.</remarks>
public enum SessionConsistencyModel
{
    /// <summary>Reads observe the latest committed state across supported readers.</summary>
    Strong,
    /// <summary>A writer immediately observes its own committed state while other readers may lag.</summary>
    ReadYourWrites,
    /// <summary>Readers may observe a stale committed replica before convergence.</summary>
    Eventual,
}
