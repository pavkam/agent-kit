// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

public sealed class CompactionStrategyKeyTests: Conformance.StringIdentityConformanceTests<CompactionStrategyKey>
{

    /// <inheritdoc/>
    protected override CompactionStrategyKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(CompactionStrategyKey subject) => subject.Value;
}
