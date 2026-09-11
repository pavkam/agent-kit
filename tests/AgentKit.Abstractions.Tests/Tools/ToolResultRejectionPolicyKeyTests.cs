// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolResultRejectionPolicyKey behavior and contracts.</summary>
public sealed class ToolResultRejectionPolicyKeyTests: Conformance.StringIdentityConformanceTests<ToolResultRejectionPolicyKey>
{
    [Fact]
    public void ToolResultRejectionPolicyKey_ToString_WhenDefault_ReturnsEmptyText() => default(ToolResultRejectionPolicyKey).ToString().ShouldBe(string.Empty);

    /// <inheritdoc/>
    protected override ToolResultRejectionPolicyKey Create(string value) => new(value);

    /// <inheritdoc/>
    protected override string? GetValue(ToolResultRejectionPolicyKey subject) => subject.Value;
}
