// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;
/// <summary>Verifies ConfigurationSourceId behavior and contracts.</summary>
public sealed class ConfigurationSourceIdTests: Conformance.StringIdentityConformanceTests<ConfigurationSourceId>
{
    /// <inheritdoc/>
    protected override ConfigurationSourceId Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ConfigurationSourceId subject) => subject.Value;
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextValueBlank_ThrowsArgumentExceptionWithValue(string value) => Should.Throw<ArgumentException>(() => new ConfigurationSourceId(value)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenTextValueNull_ThrowsArgumentNullExceptionWithValue() => Should.Throw<ArgumentNullException>(() => new ConfigurationSourceId(null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenTextValueValid_PreservesOrdinalValueAndEquality()
    {
        new ConfigurationSourceId("A").ShouldBe(new ConfigurationSourceId("A"));
        new ConfigurationSourceId("A").ShouldNotBe(new ConfigurationSourceId("a"));
        default(ConfigurationSourceId).ToString().ShouldBe(string.Empty);
    }
}
