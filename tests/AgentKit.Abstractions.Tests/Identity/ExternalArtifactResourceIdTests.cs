// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

/// <summary>Verifies the shared string-identity contract for <see cref="ExternalArtifactResourceId"/>.</summary>
public sealed class ExternalArtifactResourceIdTests: Conformance.StringIdentityConformanceTests<ExternalArtifactResourceId>
{
    /// <inheritdoc/>
    protected override ExternalArtifactResourceId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ExternalArtifactResourceId subject) => subject.Value;
}
