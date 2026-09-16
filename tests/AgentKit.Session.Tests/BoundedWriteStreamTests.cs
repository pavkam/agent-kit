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

    [Fact]
    public void Capabilities_WhenQueried_ReportWriteOnlyFixedLengthStream()
    {
        using var stream = new BoundedWriteStream(4);

        stream.CanRead.ShouldBeFalse();
        stream.CanSeek.ShouldBeFalse();
        stream.CanWrite.ShouldBeTrue();
        stream.Length.ShouldBe(0);
        stream.Write([1, 2], 0, 2);
        stream.Length.ShouldBe(2);
        stream.Position.ShouldBe(2);
    }

    [Fact]
    public void Position_WhenSet_ThrowsNotSupportedException() =>
        Should.Throw<NotSupportedException>(() =>
        {
            using var stream = new BoundedWriteStream(4);
            stream.Position = 1;
        });

    [Fact]
    public void Flush_WhenCalled_DoesNotThrow()
    {
        using var stream = new BoundedWriteStream(4);
        Should.NotThrow(stream.Flush);
    }

    [Fact]
    public void Read_WhenCalled_ThrowsNotSupportedException()
    {
        using var stream = new BoundedWriteStream(4);
        _ = Should.Throw<NotSupportedException>(() => stream.Read(new byte[1], 0, 1));
    }

    [Fact]
    public void Seek_WhenCalled_ThrowsNotSupportedException()
    {
        using var stream = new BoundedWriteStream(4);
        _ = Should.Throw<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
    }

    [Fact]
    public void SetLength_WhenCalled_ThrowsNotSupportedException()
    {
        using var stream = new BoundedWriteStream(4);
        _ = Should.Throw<NotSupportedException>(() => stream.SetLength(1));
    }

    [Fact]
    public void Write_WhenGivenReadOnlySpanExceedingRemainingCapacity_ThrowsWithoutMutation()
    {
        using var stream = new BoundedWriteStream(2);
        stream.Write([1]);

        _ = Should.Throw<InvalidOperationException>(() => stream.Write([2, 3]));

        stream.WrittenSpan.ToArray().ShouldBe([1]);
    }
}
