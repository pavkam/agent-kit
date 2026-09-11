// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ModelIdTests: Conformance.StringIdentityConformanceTests<ModelId>
{

    /// <inheritdoc/>
    protected override ModelId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ModelId subject) => subject.Value;
}
