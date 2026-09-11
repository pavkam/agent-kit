// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

public sealed class ToolInvocationResultTests
{
    [Fact]
    public void Constructor_WhenOutcomeNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(
            () => new ToolInvocationResult(null!, []));

        exception.ParamName.ShouldBe("outcome");
    }

    [Fact]
    public void Constructor_WhenContentDefault_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new ToolInvocationResult(SuccessOutcome(), default));

        exception.ParamName.ShouldBe("content");
    }

    [Fact]
    public void Constructor_WhenValid_RoundTripsProperties()
    {
        var content = ImmutableArray.Create<ContentPart>(Text("hi"));

        var result = new ToolInvocationResult(SuccessOutcome(), content);

        result.Outcome.Kind.ShouldBe(ToolCallOutcomeKind.Success);
        result.Content.ShouldBe(content);
    }

    [Fact]
    public void Equality_WhenSameOutcomeAndContent_InstancesAreEqual()
    {
        var content = ImmutableArray.Create<ContentPart>(Text("hi"));

        var first = new ToolInvocationResult(SuccessOutcome(), content);
        var second = new ToolInvocationResult(SuccessOutcome(), content);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void Equality_WhenDifferentContent_InstancesAreNotEqual()
    {
        var first = new ToolInvocationResult(SuccessOutcome(), [Text("hi")]);
        var second = new ToolInvocationResult(SuccessOutcome(), [Text("bye")]);

        first.ShouldNotBe(second);
    }

    [Fact]
    public void Equality_WhenDifferentOutcome_InstancesAreNotEqual()
    {
        var content = ImmutableArray<ContentPart>.Empty;

        var first = new ToolInvocationResult(SuccessOutcome(), content);
        var second = new ToolInvocationResult(
            new ToolCallOutcome(ToolCallOutcomeKind.Failed, ToolTerminalStatus.InvocationFailed, SideEffectCertainty.Unknown, false, "no", ExtensionData.Empty), content);

        first.ShouldNotBe(second);
    }

    private static ToolCallOutcome SuccessOutcome() => new(ToolCallOutcomeKind.Success, ToolTerminalStatus.Succeeded, SideEffectCertainty.DefinitelyPerformed, false, null, ExtensionData.Empty);

    private static TextPart Text(string value) => new(value, TextSemantics.Plain, ExtensionData.Empty);
}
