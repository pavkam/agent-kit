// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class HookPointIdTests: Conformance.StringIdentityConformanceTests<HookPointId>
{

    /// <inheritdoc/>
    protected override HookPointId Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(HookPointId subject) => subject.Value;
}
