// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Memory;

/// <summary>Declares how strictly a memory event sink must receive events.</summary>
public enum MemoryEventDelivery
{
    /// <summary>Delivery is best effort: a sink failure is reported in the dispatch result and never changes an operation.</summary>
    Observational = 0,

    /// <summary>Delivery is required: a missing or failing sink is reported as a required failure, and fail-closed callers such as the retrieval pipeline refuse to expose content without it.</summary>
    Required = 1,
}
