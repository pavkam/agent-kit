// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit;

/// <summary>One bounded validation rule outcome retained with a validated compaction.</summary>
public sealed record CompactionValidationEvidence
{
    /// <summary>Initializes a new instance of the <see cref="CompactionValidationEvidence"/> record.</summary>
    /// <param name="ruleId">The rule that produced this evidence.</param>
    /// <param name="evidenceHash">A hash of the evidence payload.</param>
    public CompactionValidationEvidence(string ruleId, ContentHash evidenceHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentOutOfRangeException.ThrowIfEqual(evidenceHash, default);
        RuleId = ruleId;
        EvidenceHash = evidenceHash;
    }

    /// <summary>Gets the rule that produced this evidence.</summary>
    public string RuleId { get; }

    /// <summary>Gets a hash of the evidence payload.</summary>
    public ContentHash EvidenceHash { get; }
}
