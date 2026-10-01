// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryStoreDescriptor"/> constraints.</summary>
public sealed class MemoryStoreDescriptorTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var descriptor = new MemoryStoreDescriptor(new MemoryStoreKey("k"), "name", new ComponentId("audience"), isDurable: true);

        descriptor.Key.ShouldBe(new MemoryStoreKey("k"));
        descriptor.Name.ShouldBe("name");
        descriptor.SecurityAudience.ShouldBe(new ComponentId("audience"));
        descriptor.IsDurable.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryStoreDescriptor(default, "n", new ComponentId("a"), false)).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenNameIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new MemoryStoreDescriptor(new MemoryStoreKey("k"), " ", new ComponentId("a"), false)).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenAudienceIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryStoreDescriptor(new MemoryStoreKey("k"), "n", default, false)).ParamName.ShouldBe("securityAudience");
}
