// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SecurityRequestIdTests: Conformance.GuidIdentityConformanceTests<SecurityRequestId>
{

    /// <inheritdoc/>
    protected override SecurityRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(SecurityRequestId subject) => subject.Value;
}
