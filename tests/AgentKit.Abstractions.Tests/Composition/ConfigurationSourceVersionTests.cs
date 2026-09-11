// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;



/// <summary>Verifies ConfigurationSourceVersion behavior and contracts.</summary>
public sealed class ConfigurationSourceVersionTests: Conformance.LongIdentityConformanceTests<ConfigurationSourceVersion>
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WhenSourceVersionNotPositive_ThrowsArgumentOutOfRangeWithValue(long value) => Should.Throw<ArgumentOutOfRangeException>(() => new ConfigurationSourceVersion(value)).ParamName.ShouldBe("value");
    [Fact]
    public void Constructor_WhenSourceVersionPositive_PreservesBoundary() => new ConfigurationSourceVersion(long.MaxValue).Value.ShouldBe(long.MaxValue);

    /// <inheritdoc/>
    protected override ConfigurationSourceVersion Create(long value) => new(value);

    /// <inheritdoc/>
    protected override long GetValue(ConfigurationSourceVersion subject) => subject.Value;

    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;
}
