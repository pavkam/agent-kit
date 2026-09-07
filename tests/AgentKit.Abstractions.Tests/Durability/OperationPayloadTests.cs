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
    public void Equality_WhenSameVersionAndBytes_InstancesAreEqual()
    {
        var left = new OperationPayload(_version, [1, 2]);
        var right = new OperationPayload(_version, [1, 2]);

        left.SchemaVersion.ShouldBe(right.SchemaVersion);
        left.Data.ShouldBe(right.Data);
    }
}
