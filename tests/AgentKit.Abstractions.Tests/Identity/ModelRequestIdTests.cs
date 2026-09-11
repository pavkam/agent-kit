// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ModelRequestIdTests: Conformance.GuidIdentityConformanceTests<ModelRequestId>
{

    /// <inheritdoc/>
    protected override ModelRequestId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ModelRequestId subject) => subject.Value;
}
