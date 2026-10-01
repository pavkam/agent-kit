// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="MemoryEventDispatchResult"/> constraints.</summary>
public sealed class MemoryEventDispatchResultTests
{
    [Fact]
    public void None_WhenRead_ReportsNothingDeliveredAndRequiredDeliveryComplete()
    {
        MemoryEventDispatchResult.None.Delivered.ShouldBe(0);
        MemoryEventDispatchResult.None.RequiredDeliveryComplete.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_WhenARequiredSinkFailed_ReportsIncompleteRequiredDelivery()
    {
        var result = new MemoryEventDispatchResult(1, 2, 3);

        result.Delivered.ShouldBe(1);
        result.ObservationalFailures.ShouldBe(2);
        result.RequiredFailures.ShouldBe(3);
        result.RequiredDeliveryComplete.ShouldBeFalse();
    }

    [Theory]
    [InlineData(-1, 0, 0, "delivered")]
    [InlineData(0, -1, 0, "observationalFailures")]
    [InlineData(0, 0, -1, "requiredFailures")]
    public void Constructor_WhenACountIsNegative_ThrowsArgumentOutOfRangeException(int delivered, int observational, int required, string parameter) =>
        Should.Throw<ArgumentOutOfRangeException>(() => new MemoryEventDispatchResult(delivered, observational, required)).ParamName.ShouldBe(parameter);
}
