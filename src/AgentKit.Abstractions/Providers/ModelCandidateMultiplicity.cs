// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>Declares the candidate multiplicity of one configured provider operation.</summary>
public enum ModelCandidateMultiplicity
{
    /// <summary>The operation requests or accepts exactly one candidate.</summary>
    ExactlyOne = 0,

    /// <summary>The operation requires a separate candidate-aware result contract with explicit identities and interleaving rules.</summary>
    Multiple = 1,
}
