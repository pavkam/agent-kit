// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

/// <summary>Verifies the shared string-identity contract for <see cref="ArtifactEventSinkId"/>.</summary>
public sealed class ArtifactEventSinkIdTests: Conformance.StringIdentityConformanceTests<ArtifactEventSinkId>
{
    /// <inheritdoc/>
    protected override ArtifactEventSinkId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ArtifactEventSinkId subject) => subject.Value;
}
