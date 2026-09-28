// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context.Compaction;

/// <summary>Delivery semantics for one compaction event sink.</summary>
public enum CompactionEventDelivery
{
    /// <summary>Publication must succeed before activation proceeds.</summary>
    Required,

    /// <summary>Publication failures are ignored for activation.</summary>
    BestEffort,
}
