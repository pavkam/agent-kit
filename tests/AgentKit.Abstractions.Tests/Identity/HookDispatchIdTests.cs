// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class HookDispatchIdTests: Conformance.GuidIdentityConformanceTests<HookDispatchId>
{
    /// <inheritdoc/>
    protected override HookDispatchId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(HookDispatchId subject) => subject.Value;
}
