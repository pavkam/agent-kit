// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Deterministic deriver conditions for reusable conformance cases.</summary>
public enum DelegatedIdentityDeriverConformanceScenario
{
    /// <summary>Claims and assurance narrow successfully.</summary>
    ValidNarrow,

    /// <summary>Parent delegation chain is already at the configured depth limit.</summary>
    MaximumDepth,

    /// <summary>Parent evidence is expired before derivation.</summary>
    ExpiredParent,

    /// <summary>Derivation blocks until cancellation.</summary>
    BlockingDerivation,
}
