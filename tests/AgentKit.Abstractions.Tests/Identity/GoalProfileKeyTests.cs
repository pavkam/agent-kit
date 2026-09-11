// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

/// <summary>Verifies GoalProfileKey behavior and contracts.</summary>
public sealed class GoalProfileKeyTests: Conformance.StringIdentityConformanceTests<GoalProfileKey>
{
    /// <inheritdoc/>
    protected override GoalProfileKey Create(string value) => new(value);
    /// <inheritdoc/>
    protected override string? GetValue(GoalProfileKey subject) => subject.Value;
    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(GoalProfileKey).Value.ShouldBeNull();
        default(GoalProfileKey).ToString().ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality()
    {
        var first = new GoalProfileKey("primary");
        var second = new GoalProfileKey("primary");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        new GoalProfileKey("secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => new GoalProfileKey(value!));
        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality()
    {
        var preserved = new GoalProfileKey(" Primary ");
        preserved.ToString().ShouldBe(" Primary ");
        new GoalProfileKey("primary").ShouldNotBe(new GoalProfileKey("PRIMARY"));
    }
}
