// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="MemoryPolicyProfileKey"/> behavior and contracts.</summary>
public sealed class MemoryPolicyProfileKeyTests: StringIdentityConformanceTests<MemoryPolicyProfileKey>
{
    /// <inheritdoc/>
    protected override MemoryPolicyProfileKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(MemoryPolicyProfileKey subject) => subject.Value;
}
