// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;
/// <summary>Verifies ConfigurationPath behavior and contracts.</summary>
public sealed class ConfigurationPathTests: Conformance.StringIdentityConformanceTests<ConfigurationPath>
{
    /// <inheritdoc/>
    protected override ConfigurationPath Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(ConfigurationPath subject) => subject.Value;
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenTextValueBlank_ThrowsArgumentExceptionWithValue(string value) => Should.Throw<ArgumentException>(() => new ConfigurationPath(value)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenTextValueNull_ThrowsArgumentNullExceptionWithValue() => Should.Throw<ArgumentNullException>(() => new ConfigurationPath(null!)).ParamName.ShouldBe("value");

    [Fact]
    public void Constructor_WhenTextValueValid_PreservesOrdinalValueAndEquality()
    {
        new ConfigurationPath("Agent.Option").ToString().ShouldBe("Agent.Option");
        new ConfigurationPath("Agent.Option").ShouldNotBe(new ConfigurationPath("agent.option"));
        default(ConfigurationPath).ToString().ShouldBe(string.Empty);
    }
}
