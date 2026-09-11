// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SandboxProfileIdTests: Conformance.StringIdentityConformanceTests<SandboxProfileId>
{

    /// <inheritdoc/>
    protected override SandboxProfileId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(SandboxProfileId subject) => subject.Value;
}
