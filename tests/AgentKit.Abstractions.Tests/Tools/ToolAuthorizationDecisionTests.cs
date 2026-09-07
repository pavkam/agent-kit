// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

public sealed class ToolAuthorizationDecisionTests
{
    [Fact]
    public void ToolAuthorizationGranted_Equality_WhenBothInstances_AreEqual()
    {
        new ToolAuthorizationGranted().ShouldBe(new ToolAuthorizationGranted());
        new ToolAuthorizationGranted().GetHashCode().ShouldBe(new ToolAuthorizationGranted().GetHashCode());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ToolAuthorizationDenied_Constructor_WhenSafeMessageInvalid_Throws(string? safeMessage) => _ = Should.Throw<ArgumentException>(() => new ToolAuthorizationDenied(safeMessage!));

    [Fact]
    public void ToolAuthorizationDenied_Constructor_WhenValid_RoundTripsSafeMessage()
    {
        var denied = new ToolAuthorizationDenied("not allowed");

        denied.SafeMessage.ShouldBe("not allowed");
    }

    [Fact]
    public void ToolAuthorizationDenied_Equality_WhenSameMessage_InstancesAreEqual() => new ToolAuthorizationDenied("no").ShouldBe(new ToolAuthorizationDenied("no"));

    [Fact]
    public void ToolAuthorizationDenied_Equality_WhenDifferentMessage_InstancesAreNotEqual() => new ToolAuthorizationDenied("no").ShouldNotBe(new ToolAuthorizationDenied("nope"));

    [Fact]
    public void ToolAuthorizationDecision_Hierarchy_EveryLeafDerivesFromToolAuthorizationDecision()
    {
        ToolAuthorizationDecision granted = new ToolAuthorizationGranted();
        ToolAuthorizationDecision denied = new ToolAuthorizationDenied("no");

        _ = granted.ShouldBeOfType<ToolAuthorizationGranted>();
        _ = denied.ShouldBeOfType<ToolAuthorizationDenied>();
    }
}
