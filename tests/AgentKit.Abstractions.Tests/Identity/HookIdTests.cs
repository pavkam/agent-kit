// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class HookIdTests: Conformance.StringIdentityConformanceTests<HookId>
{

    /// <inheritdoc/>
    protected override HookId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(HookId subject) => subject.Value;
}
