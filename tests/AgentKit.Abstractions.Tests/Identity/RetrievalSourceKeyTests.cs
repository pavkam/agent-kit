// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="RetrievalSourceKey"/> behavior and contracts.</summary>
public sealed class RetrievalSourceKeyTests: StringIdentityConformanceTests<RetrievalSourceKey>
{
    /// <inheritdoc/>
    protected override RetrievalSourceKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(RetrievalSourceKey subject) => subject.Value;
}
