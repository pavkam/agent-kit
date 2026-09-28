// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="MemoryId"/> behavior and contracts.</summary>
public sealed class MemoryIdTests: GuidIdentityConformanceTests<MemoryId>
{
    /// <inheritdoc/>
    protected override MemoryId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(MemoryId subject) => subject.Value;
}
