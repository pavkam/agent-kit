// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Deterministic issuer conditions for reusable conformance cases.</summary>
public enum IdentityIssuerConformanceScenario
{
    /// <summary>Evidence is accepted when not revoked.</summary>
    Valid,

    /// <summary>Evidence is rejected as revoked.</summary>
    Revoked,

    /// <summary>Validation blocks until cancellation.</summary>
    BlockingValidation,
}
