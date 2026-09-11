// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ArtifactVersionTests: Conformance.StringIdentityConformanceTests<ArtifactVersion>
{

    /// <inheritdoc/>
    protected override ArtifactVersion Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ArtifactVersion subject) => subject.Value;
}
