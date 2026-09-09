// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares a tool's untrusted scheduling compatibility hint.</summary>
/// <remarks>Host policy may tighten any declared mode. An unspecified mode supplies no evidence of parallel safety.</remarks>
public enum ToolSchedulingMode
{
    /// <summary>No scheduling compatibility has been asserted.</summary>
    Unspecified,
    /// <summary>The tool may overlap calls that satisfy all other policy constraints.</summary>
    ParallelSafe,
    /// <summary>The tool executes alone as an ordering barrier.</summary>
    Sequential,
    /// <summary>The tool may overlap calls except those carrying the same concurrency key.</summary>
    ConcurrencyKey,
    /// <summary>The tool executes alone within the configured scheduler scope.</summary>
    GlobalExclusive,
    /// <summary>An external host scheduler supplies equivalent ordering guarantees.</summary>
    HostScheduled,
}
