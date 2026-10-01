// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Observation;

/// <summary>Verifies <see cref="ObservationExporterKey"/> validation and value semantics.</summary>
public sealed class ObservationExporterKeyTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsBlank_ThrowsArgumentExceptionNamingValue(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new ObservationExporterKey(value!));

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_WhenValueIsProvided_PreservesExactTextAndOrdinalEquality()
    {
        var key = new ObservationExporterKey("otel-primary");

        key.Value.ShouldBe("otel-primary");
        key.ShouldBe(new ObservationExporterKey("otel-primary"));
        key.ShouldNotBe(new ObservationExporterKey("OTEL-PRIMARY"));
        key.ToString().ShouldBe("otel-primary");
    }

    [Fact]
    public void ToString_WhenInstanceIsDefault_ReturnsEmptyText() => default(ObservationExporterKey).ToString().ShouldBeEmpty();
}
