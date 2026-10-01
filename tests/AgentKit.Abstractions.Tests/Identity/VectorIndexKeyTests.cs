// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="VectorIndexKey"/> behavior and contracts.</summary>
public sealed class VectorIndexKeyTests: StringIdentityConformanceTests<VectorIndexKey>
{
    /// <inheritdoc/>
    protected override VectorIndexKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(VectorIndexKey subject) => subject.Value;
}
