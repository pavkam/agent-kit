// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class HookInvocationIdTests: Conformance.GuidIdentityConformanceTests<HookInvocationId>
{

    /// <inheritdoc/>
    protected override HookInvocationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(HookInvocationId subject) => subject.Value;
}
