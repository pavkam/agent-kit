// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>States when a context source is evaluated while assembling model requests.</summary>
/// <remarks>The frequency controls evaluation cadence only; it does not define cache ownership or freshness.</remarks>
public enum ContextEvaluationFrequency
{
    /// <summary>Evaluate once for the owning run and reuse that immutable result within the run.</summary>
    OncePerRun,

    /// <summary>Evaluate separately for each model request assembled within the run.</summary>
    OncePerModelRequest,
}
