// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Context;

/// <summary>Trust invariants enforced while merging contributor candidates.</summary>
internal static class ContextTrustRules
{
    /// <summary>Returns a safe failure message when a candidate violates instruction-trust rules; otherwise <see langword="null"/>.</summary>
    /// <param name="candidate">The candidate to validate.</param>
    /// <returns>A nonblank message when the candidate must be rejected.</returns>
    internal static string? ValidateCandidate(ContextCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return candidate.Kind != ContextCandidateKind.Instruction
            ? null
            : candidate.Trust is ContextTrust.Framework
                or ContextTrust.HostPolicy
                or ContextTrust.AgentDefinition
                or ContextTrust.Workspace
            ? null
            : "A contributor proposed instruction authority from a trust class that cannot carry instruction precedence.";
    }
}
