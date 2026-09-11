// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ArtifactPreparationIdTests: Conformance.GuidIdentityConformanceTests<ArtifactPreparationId>
{

    /// <inheritdoc/>
    protected override ArtifactPreparationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ArtifactPreparationId subject) => subject.Value;
}
