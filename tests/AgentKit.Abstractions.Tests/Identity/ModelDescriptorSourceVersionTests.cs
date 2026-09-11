// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ModelDescriptorSourceVersionTests: Conformance.LongIdentityConformanceTests<ModelDescriptorSourceVersion>
{

    /// <inheritdoc/>
    protected override ModelDescriptorSourceVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ModelDescriptorSourceVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => false;
}
