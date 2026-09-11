// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies SessionProfileKey behavior and contracts.</summary>
public sealed class SessionProfileKeyTests: Conformance.StringIdentityConformanceTests<SessionProfileKey>
{
    /// <inheritdoc/>
    protected override SessionProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(SessionProfileKey subject) => subject.Value;
    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(SessionProfileKey).Value.ShouldBeNull();
        default(SessionProfileKey).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality()
    {
        var first = new SessionProfileKey("primary");
        var second = new SessionProfileKey("primary");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        new SessionProfileKey("secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new SessionProfileKey(value!));
        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality()
    {
        var preserved = new SessionProfileKey(" Primary ");
        preserved.ToString().ShouldBe(" Primary ");
        new SessionProfileKey("primary").ShouldNotBe(new SessionProfileKey("PRIMARY"));
    }
}
