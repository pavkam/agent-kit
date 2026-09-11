// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SecurityEnforcementIntentIdTests: Conformance.GuidIdentityConformanceTests<SecurityEnforcementIntentId>
{

    /// <inheritdoc/>
    protected override SecurityEnforcementIntentId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(SecurityEnforcementIntentId subject) => subject.Value;
}
