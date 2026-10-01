// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationBounds"/> validation.</summary>
public sealed class ObservationBoundsTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenLimitIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingLimit(int limit)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ObservationBounds(limit));

        exception.ParamName.ShouldBe("maximumBytesPerField");
    }

    [Fact]
    public void Constructor_WhenLimitIsOne_AcceptsBoundaryValue() => new ObservationBounds(1).MaximumBytesPerField.ShouldBe(1);
}
