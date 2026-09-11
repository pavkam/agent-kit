// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies HookProfileKey behavior and contracts.</summary>
public sealed class HookProfileKeyTests: Conformance.StringIdentityConformanceTests<HookProfileKey>
{
    /// <inheritdoc/>
    protected override HookProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(HookProfileKey subject) => subject.Value;
    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(HookProfileKey).Value.ShouldBeNull();
        default(HookProfileKey).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality()
    {
        var first = new HookProfileKey("primary");
        var second = new HookProfileKey("primary");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        new HookProfileKey("secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new HookProfileKey(value!));
        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality()
    {
        var preserved = new HookProfileKey(" Primary ");
        preserved.ToString().ShouldBe(" Primary ");
        new HookProfileKey("primary").ShouldNotBe(new HookProfileKey("PRIMARY"));
    }
}
