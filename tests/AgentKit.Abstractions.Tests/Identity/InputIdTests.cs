// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class InputIdTests: Conformance.GuidIdentityConformanceTests<InputId>
{

    /// <inheritdoc/>
    protected override InputId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(InputId subject) => subject.Value;
}
