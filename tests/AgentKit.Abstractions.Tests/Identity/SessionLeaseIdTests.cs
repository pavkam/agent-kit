// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SessionLeaseIdTests: Conformance.GuidIdentityConformanceTests<SessionLeaseId>
{

    /// <inheritdoc/>
    protected override SessionLeaseId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(SessionLeaseId subject) => subject.Value;
}
