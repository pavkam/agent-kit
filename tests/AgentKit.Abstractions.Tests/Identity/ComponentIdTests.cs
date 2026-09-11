// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class ComponentIdTests: Conformance.StringIdentityConformanceTests<ComponentId>
{

    /// <inheritdoc/>
    protected override ComponentId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ComponentId subject) => subject.Value;
}
