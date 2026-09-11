// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class SessionIdTests: Conformance.GuidIdentityConformanceTests<SessionId>
{

    /// <inheritdoc/>
    protected override SessionId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(SessionId subject) => subject.Value;
}
