// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

using AgentKit.TestSupport;

/// <summary>Verifies <see cref="MemoryPolicyContext"/> constraints.</summary>
public sealed class MemoryPolicyContextTests
{
    [Fact]
    public void Constructor_WhenValid_PreservesProfileAndInstant()
    {
        var profile = MemoryTestData.Snapshot();

        var context = new MemoryPolicyContext(profile, MemoryTestData.Now);

        context.Profile.ShouldBe(profile);
        context.Now.ShouldBe(MemoryTestData.Now);
    }

    [Fact]
    public void Constructor_WhenProfileIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new MemoryPolicyContext(null!, MemoryTestData.Now)).ParamName.ShouldBe("profile");
}
