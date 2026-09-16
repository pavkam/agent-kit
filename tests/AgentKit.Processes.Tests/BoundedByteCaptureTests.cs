// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Processes.Tests;

/// <summary>Verifies BoundedByteCapture behavior and contracts.</summary>
public sealed class BoundedByteCaptureTests
{
    [Fact]
    public void Constructor_WhenCapacityIsNotPositive_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new BoundedByteCapture(0));

    [Fact]
    public void Append_WhenWithinCapacity_RetainsCompleteBytesInOrder()
    {
        var capture = new BoundedByteCapture(8);
        capture.Append([1, 2, 3]);
        capture.Append([4, 5]);
        capture.IsComplete.ShouldBeTrue();
        capture.ToImmutableArray().ShouldBe<byte>([1, 2, 3, 4, 5]);
    }

    [Fact]
    public void Append_WhenEmptySpanProvided_IsANoOp()
    {
        var capture = new BoundedByteCapture(4);
        capture.Append([]);
        capture.IsComplete.ShouldBeTrue();
        capture.ToImmutableArray().ShouldBeEmpty();
    }

    [Fact]
    public void Append_WhenCapacityExceeded_AbandonsCaptureIrreversibly()
    {
        var capture = new BoundedByteCapture(4);
        capture.Append([1, 2, 3, 4, 5]);
        capture.IsComplete.ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => capture.ToImmutableArray());
    }

    [Fact]
    public void Append_WhenCalledAfterAbandonment_RemainsANoOp()
    {
        var capture = new BoundedByteCapture(2);
        capture.Append([1, 2, 3]);
        capture.IsComplete.ShouldBeFalse();
        capture.Append([4, 5]);
        capture.IsComplete.ShouldBeFalse();
        _ = Should.Throw<InvalidOperationException>(() => capture.ToImmutableArray());
    }
}
