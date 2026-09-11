// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

public sealed class ToolInvokerUnavailableTests
{
    [Fact]
    public void Constructor_WhenIdentityIsDefault_RejectsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolInvokerUnavailable(default, "unavailable"));
        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("identity");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void Constructor_WhenReasonIsInvalid_RejectsExactParameter(string? reason)
    {
        var exception = Should.Throw<ArgumentException>(() => new ToolInvokerUnavailable(Identity(), reason!));
        exception.GetType().ShouldBe(reason is null ? typeof(ArgumentNullException) : typeof(ArgumentException));
        exception.ParamName.ShouldBe("safeReason");
    }

    [Fact]
    public void Equals_WhenIdentityVersionOrReasonChanges_PreservesExactEvidence()
    {
        var first = new ToolInvokerUnavailable(Identity(), "unavailable");
        var equal = new ToolInvokerUnavailable(Identity(), "unavailable");
        first.Identity.ShouldBe(Identity());
        first.SafeReason.ShouldBe("unavailable");
        first.ShouldBe(equal);
        (first with { }).ShouldBe(equal);
        first.GetHashCode().ShouldBe(equal.GetHashCode());
        first.ShouldNotBe(new ToolInvokerUnavailable(new(new ToolId("Tool"), new ToolVersion("1")), "unavailable"));
        first.ShouldNotBe(new ToolInvokerUnavailable(new(new ToolId("tool"), new ToolVersion("2")), "unavailable"));
        first.ShouldNotBe(new ToolInvokerUnavailable(Identity(), "closed"));
    }

    private static ToolIdentity Identity() => new(new ToolId("tool"), new ToolVersion("1"));
}
