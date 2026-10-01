// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;
using AgentKit.Conformance;

/// <summary>Verifies <see cref="QueryRewriterVersion"/> behavior and contracts.</summary>
public sealed class QueryRewriterVersionTests: StringIdentityConformanceTests<QueryRewriterVersion>
{
    /// <inheritdoc/>
    protected override QueryRewriterVersion Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(QueryRewriterVersion subject) => subject.Value;
}
