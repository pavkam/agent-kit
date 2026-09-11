// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class PrincipalIdTests: Conformance.StringIdentityConformanceTests<PrincipalId>
{

    /// <inheritdoc/>
    protected override PrincipalId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(PrincipalId subject) => subject.Value;
}
