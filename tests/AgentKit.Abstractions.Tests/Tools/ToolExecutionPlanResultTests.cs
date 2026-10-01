// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies <see cref="ToolExecutionPlanResult"/>.</summary>
public sealed class ToolExecutionPlanResultTests
{
    [Fact]
    public void Constructor_WhenForeignOutcomeIsCreated_RejectsTheUnsupportedFamily() =>
        Should.Throw<ArgumentException>(() => new ForeignToolExecutionPlanResult()).ParamName.ShouldBe("result");

    [Fact]
    public void CopyConstructor_WhenForeignOutcomeCopiesABuiltIn_RejectsTheUnsupportedFamily() =>
        Should.Throw<ArgumentException>(() => new ForeignToolExecutionPlanResult(new ToolExecutionPlanRejected("no"))).ParamName.ShouldBe("result");

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentNullException>(() => new ForeignToolExecutionPlanResult(null!)).ParamName.ShouldBe("original");
}
