// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;
/// <summary>Verifies SecurityProfileVersion behavior and contracts.</summary>
public sealed class SecurityProfileVersionTests: Conformance.LongIdentityConformanceTests<SecurityProfileVersion>
{
    /// <inheritdoc/>
    protected override SecurityProfileVersion Create(long value) => new(value);
    /// <inheritdoc/>
    protected override long GetValue(SecurityProfileVersion subject) => subject.Value;
    /// <inheritdoc/>
    protected override bool RequiresPositiveValue => true;

    /// <summary>Verifies published profile and configuration versions must be positive.</summary>
    /// <param name = "value">The invalid version value.</param>
    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Constructor_WhenPublishedVersionIsNotPositive_ThrowsExactArgument(long value) => Should.Throw<ArgumentOutOfRangeException>(() => new SecurityProfileVersion(value)).ParamName.ShouldBe("value");

    /// <summary>Verifies valid scalar values preserve their exact identity and diagnostic representation.</summary>
    [Fact]
    public void Constructor_WhenScalarValuesAreValid_PreservesValues()
    {
        new SecurityProfileVersion(7).ToString().ShouldBe("7");
        default(SecurityProfileVersion).Value.ShouldBe(0);
    }
}
