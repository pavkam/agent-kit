// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="MemoryStoreKey"/> behavior and contracts.</summary>
public sealed class MemoryStoreKeyTests: StringIdentityConformanceTests<MemoryStoreKey>
{
    /// <inheritdoc/>
    protected override MemoryStoreKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(MemoryStoreKey subject) => subject.Value;
}
