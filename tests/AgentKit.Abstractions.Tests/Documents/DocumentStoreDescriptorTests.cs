// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Documents;

/// <summary>Verifies <see cref="DocumentStoreDescriptor"/> constraints.</summary>
public sealed class DocumentStoreDescriptorTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesEveryValue()
    {
        var descriptor = new DocumentStoreDescriptor(new DocumentStoreKey("k"), "name", new ComponentId("audience"), true);

        descriptor.Key.ShouldBe(new DocumentStoreKey("k"));
        descriptor.Name.ShouldBe("name");
        descriptor.SecurityAudience.ShouldBe(new ComponentId("audience"));
        descriptor.IsDurable.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenKeyIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentStoreDescriptor(default, "n", new ComponentId("a"), false)).ParamName.ShouldBe("key");

    [Fact]
    public void Constructor_WhenNameIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DocumentStoreDescriptor(new DocumentStoreKey("k"), " ", new ComponentId("a"), false)).ParamName.ShouldBe("name");

    [Fact]
    public void Constructor_WhenAudienceIsDefault_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DocumentStoreDescriptor(new DocumentStoreKey("k"), "n", default, false)).ParamName.ShouldBe("securityAudience");
}
