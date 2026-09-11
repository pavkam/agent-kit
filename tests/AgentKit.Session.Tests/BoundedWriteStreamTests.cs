// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Session.Tests;
/// <summary>Verifies BoundedWriteStream behavior and contracts.</summary>
public sealed class BoundedWriteStreamTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BoundedWriteStreamConstructor_WhenCapacityIsNotPositive_ThrowsExactParameterName(int capacity)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new BoundedWriteStream(capacity));
        exception.ParamName.ShouldBe("capacity");
    }

    [Fact]
    public void BoundedWriteStreamWrite_WhenArgumentsAreInvalid_ThrowsExactParameterName()
    {
        using var stream = new BoundedWriteStream(4);
        var bytes = new byte[2];
        Should.Throw<ArgumentNullException>(() => stream.Write(null!, 0, 0)).ParamName.ShouldBe("buffer");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, -1, 0)).ParamName.ShouldBe("offset");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, 0, -1)).ParamName.ShouldBe("count");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, 3, 0)).ParamName.ShouldBe("offset");
        Should.Throw<ArgumentOutOfRangeException>(() => stream.Write(bytes, 1, 2)).ParamName.ShouldBe("count");
        stream.WrittenSpan.ToArray().ShouldBeEmpty();
    }

    [Fact]
    public void BoundedWriteStreamWrite_WhenFillingRemainingCapacity_PreservesBytesAndRejectsLaterWriteWithoutMutation()
    {
        using var stream = new BoundedWriteStream(4);
        stream.Write([1, 2], 0, 2);
        stream.Write([3, 4], 0, 2);
        stream.WrittenSpan.ToArray().ShouldBe([1, 2, 3, 4]);
        _ = Should.Throw<InvalidOperationException>(() => stream.Write([5], 0, 1));
        stream.WrittenSpan.ToArray().ShouldBe([1, 2, 3, 4]);
    }
}
