// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies DenseFloatVector behavior and contracts.</summary>
public sealed class DenseFloatVectorTests
{
    [Fact]
    public void DenseFloatVector_Constructor_WhenValuesEmpty_ThrowsArgumentException() => _ = Should.Throw<ArgumentException>(() => new DenseFloatVector([]));
    [Fact]
    public void DenseFloatVector_Equality_WhenSameValues_InstancesAreEqual()
    {
        var first = new DenseFloatVector([1.0f, 2.0f]);
        var second = new DenseFloatVector([1.0f, 2.0f]);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void DenseFloatVector_Equality_WhenDifferentValues_InstancesAreNotEqual() => new DenseFloatVector([1.0f]).ShouldNotBe(new DenseFloatVector([2.0f]));
}
