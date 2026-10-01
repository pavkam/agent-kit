// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="ChunkId"/> behavior and contracts.</summary>
public sealed class ChunkIdTests: GuidIdentityConformanceTests<ChunkId>
{
    /// <inheritdoc/>
    protected override ChunkId Create(Guid value) => new(value);

    /// <inheritdoc/>
    protected override Guid GetValue(ChunkId subject) => subject.Value;
}
