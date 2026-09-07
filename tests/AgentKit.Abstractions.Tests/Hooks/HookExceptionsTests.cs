// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Hooks;

using AgentKit;

public sealed class HookExceptionsTests
{
    public static TheoryData<Func<Exception>> ParameterlessFactories => new()
    {
        () => new HookCompositionException(),
        () => new HookReentrancyException(),
        () => new HookValidationException()
    };

    public static TheoryData<Func<string, Exception>> MessageFactories => new()
    {
        message => new HookCompositionException(message),
        message => new HookReentrancyException(message),
        message => new HookValidationException(message)
    };

    public static TheoryData<Func<string, Exception, Exception>> MessageAndInnerFactories => new()
    {
        (message, inner) => new HookCompositionException(message, inner),
        (message, inner) => new HookReentrancyException(message, inner),
        (message, inner) => new HookValidationException(message, inner)
    };

    [Theory]
    [MemberData(nameof(ParameterlessFactories))]
    public void Constructor_WhenParameterless_ProducesUsableException(Func<Exception> factory)
    {
        var exception = factory();

        _ = exception.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(MessageFactories))]
    public void Constructor_WhenMessageOnly_SetsMessage(Func<string, Exception> factory)
    {
        var exception = factory("something went wrong");

        exception.Message.ShouldBe("something went wrong");
        exception.InnerException.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(MessageAndInnerFactories))]
    public void Constructor_WhenMessageAndInnerException_SetsBoth(Func<string, Exception, Exception> factory)
    {
        var inner = new InvalidOperationException("cause");

        var exception = factory("something went wrong", inner);

        exception.Message.ShouldBe("something went wrong");
        exception.InnerException.ShouldBeSameAs(inner);
    }
}
