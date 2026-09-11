// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ArtifactOwnerIdTests: Conformance.StringIdentityConformanceTests<ArtifactOwnerId>
{

    /// <inheritdoc/>
    protected override ArtifactOwnerId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ArtifactOwnerId subject) => subject.Value;
}
