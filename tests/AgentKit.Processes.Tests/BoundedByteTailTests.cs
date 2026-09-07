// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

public sealed class BoundedByteTailTests
{
    [Fact]
    public void Append_WhenWritesWrapSeveralTimes_RetainsExactNewestSuffix()
    {
        var tail = new BoundedByteTail(4);

        tail.Append("12"u8);
        tail.Append("345"u8);
        tail.Append("6789"u8);

        tail.TotalBytes.ShouldBe(9);
        tail.IsTruncated.ShouldBeTrue();
        Encoding.UTF8.GetString(tail.ToImmutableArray().AsSpan()).ShouldBe("6789");
    }

    [Fact]
    public void Constructor_WhenCapacityZero_ThrowsExactConstraint()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BoundedByteTail(0));

        exception.ParamName.ShouldBe("capacity");
    }
}
