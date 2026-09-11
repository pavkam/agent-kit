// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

/// <summary>Verifies HookCompositionException behavior and contracts.</summary>
public sealed class HookCompositionExceptionTests
{
    [Fact]
    public void Constructor_WhenParameterless_ProducesUsableException()
    {
        var exception = new HookCompositionException();
        _ = exception.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WhenMessageOnly_SetsMessage()
    {
        var exception = new HookCompositionException("something went wrong");
        exception.Message.ShouldBe("something went wrong");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WhenMessageAndInnerException_SetsBoth()
    {
        var inner = new InvalidOperationException("cause");
        var exception = new HookCompositionException("something went wrong", inner);
        exception.Message.ShouldBe("something went wrong");
        exception.InnerException.ShouldBeSameAs(inner);
    }
}
