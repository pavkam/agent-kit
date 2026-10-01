// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolExecutionPolicyUnavailable"/>.</summary>
public sealed class ToolExecutionPolicyUnavailableTests
{
    [Fact]
    public void Constructor_WhenReferenceIsValid_RetainsTheExactReference() =>
        new ToolExecutionPolicyUnavailable(ExecutionPolicy()).Reference.ShouldBe(ExecutionPolicy());

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPolicyUnavailable(null!)).ParamName.ShouldBe("reference");
}
