// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="QueryRewriterKey"/> behavior and contracts.</summary>
public sealed class QueryRewriterKeyTests: StringIdentityConformanceTests<QueryRewriterKey>
{
    /// <inheritdoc/>
    protected override QueryRewriterKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(QueryRewriterKey subject) => subject.Value;
}
