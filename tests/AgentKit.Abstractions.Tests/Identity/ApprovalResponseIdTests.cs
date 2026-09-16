// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies ApprovalResponseId behavior and contracts.</summary>
public sealed class ApprovalResponseIdTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesValue()
    {
        var guid = Guid.NewGuid();
        var id = new ApprovalResponseId(guid);
        id.Value.ShouldBe(guid);
    }

    [Fact]
    public void Constructor_WhenValueIsEmpty_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ApprovalResponseId(Guid.Empty));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void ToString_WhenCalled_ReturnsCompactFormat()
    {
        var guid = Guid.NewGuid();
        var id = new ApprovalResponseId(guid);
        id.ToString().ShouldBe(guid.ToString("N", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Equals_WhenSameValue_InstancesAreEqual()
    {
        var guid = Guid.NewGuid();
        var first = new ApprovalResponseId(guid);
        var second = new ApprovalResponseId(guid);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
