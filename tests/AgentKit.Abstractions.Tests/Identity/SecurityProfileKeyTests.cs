// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies SecurityProfileKey behavior and contracts.</summary>
public sealed class SecurityProfileKeyTests: Conformance.StringIdentityConformanceTests<SecurityProfileKey>
{
    /// <inheritdoc/>
    protected override SecurityProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(SecurityProfileKey subject) => subject.Value;
    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(SecurityProfileKey).Value.ShouldBeNull();
        default(SecurityProfileKey).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality()
    {
        var first = new SecurityProfileKey("primary");
        var second = new SecurityProfileKey("primary");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        new SecurityProfileKey("secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new SecurityProfileKey(value!));
        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality()
    {
        var preserved = new SecurityProfileKey(" Primary ");
        preserved.ToString().ShouldBe(" Primary ");
        new SecurityProfileKey("primary").ShouldNotBe(new SecurityProfileKey("PRIMARY"));
    }
}
