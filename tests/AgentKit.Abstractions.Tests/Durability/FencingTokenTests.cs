// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using System.Globalization;

using AgentKit;

/// <summary>
/// Exercises the fencing token's allocation guard and its ordering contract,
/// which is what lets a store reject a stale worker's write.
/// </summary>
public sealed class FencingTokenTests: Conformance.LongIdentityConformanceTests<FencingToken>
{
    [Fact]
    public void Constructor_WhenValueIsZero_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FencingToken(0));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsNegative_ThrowsArgumentOutOfRangeException()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FencingToken(-1));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsSmallestAllocated_Succeeds() =>
        new FencingToken(1).Value.ShouldBe(1);

    [Fact]
    public void Constructor_WhenValueIsMaximum_Succeeds() =>
        new FencingToken(long.MaxValue).Value.ShouldBe(long.MaxValue);

    [Fact]
    public void Default_IsNotAValidAllocatedGeneration() =>
        default(FencingToken).Value.ShouldBe(0);

    [Fact]
    public void CompareTo_WhenTokenIsOlder_ReturnsNegative() =>
        new FencingToken(1).CompareTo(new FencingToken(2)).ShouldBeLessThan(0);

    [Fact]
    public void CompareTo_WhenTokenIsNewer_ReturnsPositive() =>
        new FencingToken(3).CompareTo(new FencingToken(2)).ShouldBeGreaterThan(0);

    [Fact]
    public void CompareTo_WhenSameGeneration_ReturnsZero() =>
        new FencingToken(2).CompareTo(new FencingToken(2)).ShouldBe(0);

    [Fact]
    public void LessThan_WhenTokenIsStale_IsTrue() =>
        (new FencingToken(1) < new FencingToken(2)).ShouldBeTrue();

    [Fact]
    public void GreaterThan_WhenTokenIsCurrent_IsTrue() =>
        (new FencingToken(2) > new FencingToken(1)).ShouldBeTrue();

    [Fact]
    public void LessThanOrEqual_WhenSameGeneration_IsTrue() =>
        (new FencingToken(2) <= new FencingToken(2)).ShouldBeTrue();

    [Fact]
    public void GreaterThanOrEqual_WhenSameGeneration_IsTrue() =>
        (new FencingToken(2) >= new FencingToken(2)).ShouldBeTrue();

    [Fact]
    public void Equality_WhenSameGeneration_InstancesAreEqual() =>
        new FencingToken(7).ShouldBe(new FencingToken(7));

    [Fact]
    public void ToString_ReturnsInvariantCultureText() =>
        new FencingToken(42).ToString().ShouldBe(42.ToString(CultureInfo.InvariantCulture));

    /// <inheritdoc/>
    protected override FencingToken Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(FencingToken subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
