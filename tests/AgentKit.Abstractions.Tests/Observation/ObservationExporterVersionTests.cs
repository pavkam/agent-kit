// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationExporterVersion"/> validation and formatting.</summary>
public sealed class ObservationExporterVersionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void Constructor_WhenValueIsNotPositive_ThrowsArgumentOutOfRangeExceptionNamingValue(long value)
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new ObservationExporterVersion(value));

        exception.ParamName.ShouldBe("value");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(long.MaxValue)]
    public void Constructor_WhenValueIsPositive_PreservesValueAndFormatsInvariantly(long value)
    {
        var version = new ObservationExporterVersion(value);

        version.Value.ShouldBe(value);
        version.ToString().ShouldBe(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
