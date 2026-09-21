// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Deterministic validation-policy conditions for reusable conformance cases.</summary>
public enum IdentityValidationPolicyConformanceScenario
{
    /// <summary>Evidence timing and issuer checks succeed.</summary>
    Valid,

    /// <summary>Evaluation instant equals the exclusive expiry boundary.</summary>
    ExpiredAtEquality,

    /// <summary>Authentication instant is beyond configured clock skew.</summary>
    FutureAuthenticatedAt,

    /// <summary>Evidence exceeds the configured maximum lifetime.</summary>
    LifetimeExceeded,

    /// <summary>Validation blocks until cancellation.</summary>
    BlockingValidation,
}
