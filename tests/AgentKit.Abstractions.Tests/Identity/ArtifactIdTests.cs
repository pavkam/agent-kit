// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ArtifactIdTests: Conformance.GuidIdentityConformanceTests<ArtifactId>
{

    /// <inheritdoc/>
    protected override ArtifactId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ArtifactId subject) => subject.Value;
}
