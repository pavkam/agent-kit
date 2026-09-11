// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ModelDescriptorSourceIdTests: Conformance.StringIdentityConformanceTests<ModelDescriptorSourceId>
{

    /// <inheritdoc/>
    protected override ModelDescriptorSourceId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ModelDescriptorSourceId subject) => subject.Value;
}
