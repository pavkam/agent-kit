// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Identity;

using AgentKit;

public sealed class ProfileKeyTests
{
    [Theory]
    [InlineData("budget")]
    [InlineData("session")]
    [InlineData("hook")]
    [InlineData("security")]
    [InlineData("memory")]
    [InlineData("goal")]
    public void Constructor_WhenValueIsValid_RoundTripsWithStructuralEquality(string kind)
    {
        var first = Create(kind, "primary");
        var second = Create(kind, "primary");

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
        first.ToString().ShouldBe("primary");
        Create(kind, "secondary").ShouldNotBe(first);
    }

    [Theory]
    [InlineData("budget", null)]
    [InlineData("budget", "")]
    [InlineData("budget", " ")]
    [InlineData("session", null)]
    [InlineData("session", "")]
    [InlineData("session", " ")]
    [InlineData("hook", null)]
    [InlineData("hook", "")]
    [InlineData("hook", " ")]
    [InlineData("security", null)]
    [InlineData("security", "")]
    [InlineData("security", " ")]
    [InlineData("memory", null)]
    [InlineData("memory", "")]
    [InlineData("memory", " ")]
    [InlineData("goal", null)]
    [InlineData("goal", "")]
    [InlineData("goal", " ")]
    public void Constructor_WhenValueIsBlank_ThrowsExactArgumentException(
        string kind,
        string? value)
    {
        var exception = Should.Throw<ArgumentException>(() => Create(kind, value!));

        _ = value is null ? exception.ShouldBeOfType<ArgumentNullException>() : exception.ShouldBeOfType<ArgumentException>();

        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void DefaultValues_HaveNoUsableSelectionText()
    {
        default(BudgetProfileKey).Value.ShouldBeNull();
        default(BudgetProfileKey).ToString().ShouldBeEmpty();
        default(SessionProfileKey).Value.ShouldBeNull();
        default(SessionProfileKey).ToString().ShouldBeEmpty();
        default(HookProfileKey).Value.ShouldBeNull();
        default(HookProfileKey).ToString().ShouldBeEmpty();
        default(SecurityProfileKey).Value.ShouldBeNull();
        default(SecurityProfileKey).ToString().ShouldBeEmpty();
        default(MemoryProfileKey).Value.ShouldBeNull();
        default(MemoryProfileKey).ToString().ShouldBeEmpty();
        default(GoalProfileKey).Value.ShouldBeNull();
        default(GoalProfileKey).ToString().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("budget")]
    [InlineData("session")]
    [InlineData("hook")]
    [InlineData("security")]
    [InlineData("memory")]
    [InlineData("goal")]
    public void Constructor_PreservesExactTextAndUsesOrdinalCaseSensitiveEquality(string kind)
    {
        var preserved = Create(kind, " Primary ");

        preserved.ToString().ShouldBe(" Primary ");
        Create(kind, "primary").ShouldNotBe(Create(kind, "PRIMARY"));
    }

    private static object Create(string kind, string value) => kind switch
    {
        "budget" => new BudgetProfileKey(value),
        "session" => new SessionProfileKey(value),
        "hook" => new HookProfileKey(value),
        "security" => new SecurityProfileKey(value),
        "memory" => new MemoryProfileKey(value),
        "goal" => new GoalProfileKey(value),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown profile-key kind."),
    };
}
