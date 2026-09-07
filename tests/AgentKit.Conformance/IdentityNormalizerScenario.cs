// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Conformance;

/// <summary>Chooses one externally observable normalization-boundary condition for a reusable fixture.</summary>
public enum IdentityNormalizerScenario
{
    /// <summary>The issuer and policies preserve valid identity evidence.</summary>
    Valid,
    /// <summary>A policy safely narrows claims and assurance.</summary>
    Narrow,
    /// <summary>The issuer cannot produce a supported normalization result.</summary>
    IssuerUnavailable,
    /// <summary>The issuer descriptor does not match the assertion issuer.</summary>
    MalformedIssuer,
    /// <summary>A narrowing policy attempts to replace authentication evidence.</summary>
    ReplaceEvidence,
    /// <summary>A narrowing policy attempts to replace the captured identity version.</summary>
    ReplaceVersion,
    /// <summary>A narrowing policy attempts to add a claim.</summary>
    WidenClaims,
    /// <summary>A narrowing policy attempts to increase assurance.</summary>
    WidenAssurance,
    /// <summary>A narrowing policy attempts to replace the mapped tenant.</summary>
    ReplaceTenant,
    /// <summary>A narrowing policy attempts to replace the mapped principal.</summary>
    ReplacePrincipal,
    /// <summary>A narrowing policy attempts to replace the mapped subject kind.</summary>
    ReplaceSubjectKind,
    /// <summary>A narrowing policy attempts to replace the authenticated delegation chain.</summary>
    ReplaceDelegationChain,
    /// <summary>A later policy attempts to restore a claim removed by an earlier policy.</summary>
    RestoreRemovedClaim,
    /// <summary>The issuer waits for cancellation after signaling entry.</summary>
    BlockingIssuer,
}
