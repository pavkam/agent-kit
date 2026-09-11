// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class CapabilityProfileIdTests: Conformance.StringIdentityConformanceTests<CapabilityProfileId>
{

    /// <inheritdoc/>
    protected override CapabilityProfileId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(CapabilityProfileId subject) => subject.Value;
}
