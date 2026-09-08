// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>
/// Exercises the durable payload's initialization guards, including the
/// distinction between an empty payload and an uninitialized array.
/// </summary>
public sealed class OperationPayloadTests
{
    private static readonly SchemaVersion _version = new("v1");

    [Fact]
    public void Constructor_WhenDataIsDefaultArray_ThrowsArgumentException()
    {
        var exception = Should.Throw<ArgumentException>(
            () => new OperationPayload(_version, default));

        exception.ParamName.ShouldBe("data");
    }

    [Fact]
    public void Constructor_WhenDataIsEmpty_Succeeds()
    {
        var payload = new OperationPayload(_version, []);

        payload.Data.ShouldBeEmpty();
        payload.SchemaVersion.ShouldBe(_version);
    }

    [Fact]
    public void Constructor_WhenDataIsPopulated_PreservesBytes() =>
        new OperationPayload(_version, [1, 2, 3]).Data.ShouldBe([1, 2, 3]);

    [Fact]
    public void With_WhenDataIsDefaultArray_ThrowsBeforeProducingInvalidPayload()
    {
        var payload = new OperationPayload(_version, [1]);

        var exception = Should.Throw<ArgumentException>(
            () => payload with { Data = default });

        exception.ParamName.ShouldBe(nameof(OperationPayload.Data));
    }

    [Fact]
    public void Constructor_WhenSchemaVersionIsDefault_ThrowsExactArgumentException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new OperationPayload(default, []));

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe("schemaVersion");
    }

    [Fact]
    public void With_WhenSchemaVersionIsDefault_ThrowsExactArgumentExceptionAndPreservesOriginal()
    {
        var payload = new OperationPayload(_version, [1]);

        var exception = Should.Throw<ArgumentOutOfRangeException>(() => payload with { SchemaVersion = default });

        exception.GetType().ShouldBe(typeof(ArgumentOutOfRangeException));
        exception.ParamName.ShouldBe(nameof(OperationPayload.SchemaVersion));
        payload.SchemaVersion.ShouldBe(_version);
    }

    [Fact]
    public void Equality_WhenIndependentlyAllocatedBytesMatch_UsesByteSequenceAndHashEquality()
    {
        var left = new OperationPayload(_version, [1, 2]);
        var right = new OperationPayload(_version, [1, 2]);
        var values = new Dictionary<OperationPayload, string> { [left] = "payload" };
        var set = new HashSet<OperationPayload> { left, right };

        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
        values[right].ShouldBe("payload");
        set.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("length")]
    [InlineData("content")]
    public void Equality_WhenSchemaOrByteSequenceDiffers_IsNotEqual(string difference)
    {
        var baseline = new OperationPayload(_version, [1, 2]);
        var different = difference switch
        {
            "schema" => new OperationPayload(new SchemaVersion("v2"), [1, 2]),
            "length" => new OperationPayload(_version, [1]),
            "content" => new OperationPayload(_version, [1, 3]),
            _ => throw new ArgumentOutOfRangeException(nameof(difference)),
        };

        baseline.ShouldNotBe(different);
    }

    [Fact]
    public void With_WhenBytesAreReplacedWithEqualIndependentSequence_RemainsEqualAndLeavesOriginalUnchanged()
    {
        var original = new OperationPayload(_version, [4, 5]);
        var copy = original with { Data = [4, 5] };

        copy.ShouldBe(original);
        original.Data.ShouldBe([4, 5]);
        copy.Data.ShouldBe([4, 5]);
    }
}
