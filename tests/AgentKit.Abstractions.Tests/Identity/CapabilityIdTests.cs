// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class CapabilityIdTests: Conformance.StringIdentityConformanceTests<CapabilityId>
{

    /// <inheritdoc/>
    protected override CapabilityId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(CapabilityId subject) => subject.Value;
}
