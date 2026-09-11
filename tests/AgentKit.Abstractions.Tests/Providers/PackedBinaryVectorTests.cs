// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies PackedBinaryVector behavior and contracts.</summary>
public sealed class PackedBinaryVectorTests
{
    [Fact]
    public void PackedBinaryVector_Constructor_WhenValuesEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new PackedBinaryVector([], signed: false));
    [Fact]
    public void PackedBinaryVector_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new PackedBinaryVector([0b10101010], signed: false);
        var second = new PackedBinaryVector([0b10101010], signed: false);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
