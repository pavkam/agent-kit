// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ArtifactProfileKeyTests: Conformance.StringIdentityConformanceTests<ArtifactProfileKey>
{

    /// <inheritdoc/>
    protected override ArtifactProfileKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ArtifactProfileKey subject) => subject.Value;
}
