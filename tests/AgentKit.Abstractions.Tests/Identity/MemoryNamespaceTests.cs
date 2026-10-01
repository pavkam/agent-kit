// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="MemoryNamespace"/> behavior and contracts.</summary>
public sealed class MemoryNamespaceTests: StringIdentityConformanceTests<MemoryNamespace>
{
    /// <inheritdoc/>
    protected override MemoryNamespace Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(MemoryNamespace subject) => subject.Value;
}
