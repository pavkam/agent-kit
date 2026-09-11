// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using AgentKit;

/// <summary>Verifies ToolAuthorizationDenied behavior and contracts.</summary>
public sealed class ToolAuthorizationDeniedTests: Conformance.SingleMessageLeafConformanceTests<ToolAuthorizationDenied>
{
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

    /// <inheritdoc/>
    protected override ToolAuthorizationDenied Create(string message) => new(message);

    /// <inheritdoc/>
    protected override string GetValue(ToolAuthorizationDenied subject) => subject.SafeMessage;
}
