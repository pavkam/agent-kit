// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies QuantizedByteVector behavior and contracts.</summary>
public sealed class QuantizedByteVectorTests
{
    [Fact]
    public void QuantizedByteVector_Constructor_WhenValuesEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new QuantizedByteVector([], signed: true));
    [Fact]
    public void QuantizedByteVector_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new QuantizedByteVector([1, 2, 3], signed: true);
        var second = new QuantizedByteVector([1, 2, 3], signed: true);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void QuantizedByteVector_Equality_WhenDifferentSignedness_InstancesAreNotEqual() => new QuantizedByteVector([1, 2], signed: true).ShouldNotBe(new QuantizedByteVector([1, 2], signed: false));
}
