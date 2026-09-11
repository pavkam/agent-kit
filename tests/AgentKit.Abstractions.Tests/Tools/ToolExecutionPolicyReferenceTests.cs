// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;



/// <summary>Verifies ToolExecutionPolicyReference behavior and contracts.</summary>
public sealed class ToolExecutionPolicyReferenceTests
{
    [Fact]
    public void ToolExecutionPolicyReference_Constructor_WhenKeyDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyReference(default, new ToolExecutionPolicyVersion(1)));
        exception.ParamName.ShouldBe("key");
    }

    [Fact]
    public void ToolExecutionPolicyReference_Constructor_WhenVersionDefault_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void ToolExecutionPolicyReference_Constructor_WhenValuesValid_PreservesEvidence()
    {
        var reference = new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(3));
        reference.Key.ShouldBe(new ToolExecutionPolicyKey("standard"));
        reference.Version.ShouldBe(new ToolExecutionPolicyVersion(3));
        reference.ShouldBe(new ToolExecutionPolicyReference(new ToolExecutionPolicyKey("standard"), new ToolExecutionPolicyVersion(3)));
    }
}
