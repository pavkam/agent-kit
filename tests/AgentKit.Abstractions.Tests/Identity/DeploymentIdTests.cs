// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class DeploymentIdTests: Conformance.StringIdentityConformanceTests<DeploymentId>
{

    /// <inheritdoc/>
    protected override DeploymentId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(DeploymentId subject) => subject.Value;
}
