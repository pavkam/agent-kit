// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

/// <summary>Verifies the shared GUID-identity contract for <see cref="ArtifactReferenceCommitIntentId"/>.</summary>
public sealed class ArtifactReferenceCommitIntentIdTests: Conformance.GuidIdentityConformanceTests<ArtifactReferenceCommitIntentId>
{
    /// <inheritdoc/>
    protected override ArtifactReferenceCommitIntentId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ArtifactReferenceCommitIntentId subject) => subject.Value;
}
