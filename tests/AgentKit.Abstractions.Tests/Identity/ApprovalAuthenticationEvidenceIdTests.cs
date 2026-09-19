// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ApprovalAuthenticationEvidenceId behavior and contracts.</summary>
public sealed class ApprovalAuthenticationEvidenceIdTests: Conformance.StringIdentityConformanceTests<ApprovalAuthenticationEvidenceId>
{
    /// <inheritdoc/>
    protected override ApprovalAuthenticationEvidenceId Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ApprovalAuthenticationEvidenceId subject) => subject.Value;
}
