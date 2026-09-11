// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolInvokerLeaseResultTests
{
    [Fact]
    public void Constructor_WhenForeignOutcomeIsCreated_RejectsTheClosedFamily()
    {
        var exception = Should.Throw<ArgumentException>(() => new ForeignToolInvokerLeaseResult());
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("result");
    }

    [Fact]
    public void CopyConstructor_WhenForeignOutcomeCopiesKnownResult_RejectsTheClosedFamily()
    {
        var original = new ToolInvokerUnavailable(new(new ToolId("tool"), new ToolVersion("1")), "unavailable");
        var exception = Should.Throw<ArgumentException>(() => new ForeignToolInvokerLeaseResult(original));
        exception.GetType().ShouldBe(typeof(ArgumentException));
        exception.ParamName.ShouldBe("result");
    }

    [Fact]
    public void CopyConstructor_WhenOriginalIsNull_RejectsExactParameter()
    {
        var exception = Should.Throw<ArgumentNullException>(() => new ForeignToolInvokerLeaseResult(null!));
        exception.GetType().ShouldBe(typeof(ArgumentNullException));
        exception.ParamName.ShouldBe("original");
    }
}
