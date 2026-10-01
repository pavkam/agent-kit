// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

using static ToolRuntimeTestFixture;

/// <summary>Verifies <see cref="ToolExecutionPolicyBinding"/> validation.</summary>
public sealed class ToolExecutionPolicyBindingTests
{
    [Fact]
    public void Constructor_WhenReferenceIsValid_RoundTripsProperty() =>
        new ToolExecutionPolicyBinding(ExecutionPolicy()).Reference.ShouldBe(ExecutionPolicy());

    [Fact]
    public void Constructor_WhenReferenceIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ToolExecutionPolicyBinding(null!)).ParamName.ShouldBe("reference");
}
