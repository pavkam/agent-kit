// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Tools;

using System.Reflection;

using AgentKit;

/// <summary>Verifies ToolResultProjectionPolicyReference behavior and contracts.</summary>
public sealed class ToolResultProjectionPolicyReferenceTests
{
    [Fact]
    public void Reference_WhenKeyOrVersionIsDefault_ThrowsExactParameterName()
    {
        Should.Throw<ArgumentNullException>(() => new ToolResultProjectionPolicyReference(default, Version())).ParamName.ShouldBe("key");
        Should.Throw<ArgumentOutOfRangeException>(() => new ToolResultProjectionPolicyReference(Key(), default)).ParamName.ShouldBe("version");
    }

    [Fact]
    public void Reference_WhenValid_RetainsImmutableIdentity()
    {
        var reference = Reference();
        reference.Key.ShouldBe(Key());
        reference.Version.ShouldBe(Version());
        reference.ShouldBe(new ToolResultProjectionPolicyReference(Key(), Version()));
        reference.GetHashCode().ShouldBe(new ToolResultProjectionPolicyReference(Key(), Version()).GetHashCode());
    }

    private static ToolResultProjectionPolicyKey Key() => new("projection.default");
    private static ToolResultProjectionPolicyVersion Version() => new(1);
    private static ToolResultProjectionPolicyReference Reference() => new(Key(), Version());
    [Fact]
    public void PolicyRecords_WhenInspected_ExposeGetOnlyProperties() => typeof(ToolResultProjectionPolicyReference).GetProperties(BindingFlags.Instance | BindingFlags.Public).ShouldAllBe(static property => property.SetMethod == null);

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Reference();
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
