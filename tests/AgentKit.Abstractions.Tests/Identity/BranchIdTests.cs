// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class BranchIdTests: Conformance.GuidIdentityConformanceTests<BranchId>
{

    /// <inheritdoc/>
    protected override BranchId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(BranchId subject) => subject.Value;
}
