// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class OperationIdTests: Conformance.GuidIdentityConformanceTests<OperationId>
{

    /// <inheritdoc/>
    protected override OperationId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(OperationId subject) => subject.Value;
}
