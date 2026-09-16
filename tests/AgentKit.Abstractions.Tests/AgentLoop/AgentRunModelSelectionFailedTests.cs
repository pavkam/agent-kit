// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.AgentLoop;

/// <summary>Verifies AgentRunModelSelectionFailed behavior and contracts.</summary>
public sealed class AgentRunModelSelectionFailedTests
{
    [Fact]
    public void Constructor_WhenSafeReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunModelSelectionFailed(" ", [])).ParamName.ShouldBe("safeReason");

    [Fact]
    public void Constructor_WhenDiagnosticsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new AgentRunModelSelectionFailed("reason", [null!])).ParamName.ShouldBe("diagnostics");

    [Fact]
    public void Initializer_WhenSafeReasonIsBlank_ThrowsExactArgumentException()
    {
        var outcome = Outcome();
        Should.Throw<ArgumentException>(() => outcome with { SafeReason = " " }).ParamName.ShouldBe("SafeReason");
    }

    [Fact]
    public void Initializer_WhenDiagnosticsContainNull_ThrowsExactArgumentException()
    {
        var outcome = Outcome();
        Should.Throw<ArgumentException>(() => outcome with { Diagnostics = [null!] }).ParamName.ShouldBe("Diagnostics");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var outcome = Outcome();
        outcome.SafeReason.ShouldBe("no model");
        outcome.Diagnostics.ShouldBe([Diagnostic()]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = Outcome();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Initializer_WhenValuesAreValid_ReplaceExistingValues()
    {
        var original = Outcome();
        var copy = original with { SafeReason = "updated", Diagnostics = [] };
        copy.SafeReason.ShouldBe("updated");
        copy.Diagnostics.ShouldBeEmpty();
    }

    private static ModelSelectionDiagnostic Diagnostic() => new(new ModelAlias("chat"), ModelCandidateOutcome.NotInCatalog, "not found");
    private static AgentRunModelSelectionFailed Outcome() => new("no model", [Diagnostic()]);
}
