// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

/// <summary>Verifies the shared string-identity contract for <see cref="ArtifactBackendKey"/>.</summary>
public sealed class ArtifactBackendKeyTests: Conformance.StringIdentityConformanceTests<ArtifactBackendKey>
{
    /// <inheritdoc/>
    protected override ArtifactBackendKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ArtifactBackendKey subject) => subject.Value;
}
