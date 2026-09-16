// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

/// <summary>Verifies ToolResultRejectionPolicyReference behavior and contracts.</summary>
public sealed class ToolResultRejectionPolicyReferenceTests
{
    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultRejectionPolicyReference(default, new ToolResultRejectionPolicyVersion(1))).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenVersionIsDefault_ThrowsExactParameter() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("rejection"), default)).ParamName.ShouldBe("version");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var key = new ToolResultRejectionPolicyKey("rejection");
        var version = new ToolResultRejectionPolicyVersion(1);
        var reference = new ToolResultRejectionPolicyReference(key, version);
        reference.Key.ShouldBe(key);
        reference.Version.ShouldBe(version);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ToolResultRejectionPolicyReference(new ToolResultRejectionPolicyKey("rejection"), new ToolResultRejectionPolicyVersion(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
