// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ArtifactRetentionPolicyKeyTests: Conformance.StringIdentityConformanceTests<ArtifactRetentionPolicyKey>
{

    /// <inheritdoc/>
    protected override ArtifactRetentionPolicyKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ArtifactRetentionPolicyKey subject) => subject.Value;
}
