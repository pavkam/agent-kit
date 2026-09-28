// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="MemoryProfileVersion"/> behavior and contracts.</summary>
public sealed class MemoryProfileVersionTests: LongIdentityConformanceTests<MemoryProfileVersion>
{
    /// <inheritdoc/>
    protected override MemoryProfileVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(MemoryProfileVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
