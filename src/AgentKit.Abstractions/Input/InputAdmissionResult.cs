// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Defines the closed family of outcomes from one input admission attempt.</summary>
/// <remarks>The result reports durable acceptance, idempotency conflict, capacity backpressure, or pre-admission rejection. It never fabricates a run identity or indicates that accepted input has been promoted.</remarks>
public abstract record InputAdmissionResult
{
    /// <summary>Initializes one canonical admission outcome.</summary>
    /// <remarks>External assemblies cannot extend this hierarchy, allowing consumers to exhaustively distinguish acceptance, conflict, capacity, and rejection.</remarks>
    private protected InputAdmissionResult() { }
}
