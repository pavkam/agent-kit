// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies IdentityIssuerDescriptor behavior and contracts.</summary>
public sealed class IdentityIssuerDescriptorTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var id = new IdentityIssuerId("issuer");
        var version = new IdentityVersion(1);
        var descriptor = new IdentityIssuerDescriptor(id, version);
        descriptor.Id.ShouldBe(id);
        descriptor.Version.ShouldBe(version);
    }

    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new IdentityIssuerDescriptor(default, new IdentityVersion(1)));
        exception.ParamName.ShouldBe("id");
    }

    [Fact]
    public void Constructor_WhenVersionIsNotPositive_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new IdentityIssuerDescriptor(new IdentityIssuerId("issuer"), default));
        exception.ParamName.ShouldBe("version");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var id = new IdentityIssuerId("issuer");
        var version = new IdentityVersion(1);
        var first = new IdentityIssuerDescriptor(id, version);
        var second = new IdentityIssuerDescriptor(id, version);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new IdentityIssuerDescriptor(new IdentityIssuerId("issuer"), new IdentityVersion(1));
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
