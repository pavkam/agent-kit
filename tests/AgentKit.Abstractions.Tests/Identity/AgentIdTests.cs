// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class AgentIdTests: Conformance.GuidIdentityConformanceTests<AgentId>
{

    /// <inheritdoc/>
    protected override AgentId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(AgentId subject) => subject.Value;
}
