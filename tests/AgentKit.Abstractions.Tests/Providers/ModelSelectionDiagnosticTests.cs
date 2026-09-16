// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelSelectionDiagnostic behavior and contracts.</summary>
public sealed class ModelSelectionDiagnosticTests
{
    [Fact]
    public void Constructor_WhenOutcomeIsUndefined_ThrowsExactArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ModelSelectionDiagnostic(new ModelAlias("chat"), (ModelCandidateOutcome) 99, "reason")).ParamName.ShouldBe("outcome");

    [Fact]
    public void Constructor_WhenReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ModelSelectionDiagnostic(new ModelAlias("chat"), ModelCandidateOutcome.Selected, " ")).ParamName.ShouldBe("reason");

    [Fact]
    public void Initializer_WhenOutcomeIsUndefined_ThrowsExactArgumentOutOfRangeException()
    {
        var diagnostic = ProvidersTestData.Diagnostic();
        Should.Throw<ArgumentOutOfRangeException>(() => diagnostic with { Outcome = (ModelCandidateOutcome) 99 }).ParamName.ShouldBe("Outcome");
    }

    [Fact]
    public void Initializer_WhenReasonIsBlank_ThrowsExactArgumentException()
    {
        var diagnostic = ProvidersTestData.Diagnostic();
        Should.Throw<ArgumentException>(() => diagnostic with { Reason = " " }).ParamName.ShouldBe("Reason");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var diagnostic = ProvidersTestData.Diagnostic();
        diagnostic.Alias.ShouldBe(new ModelAlias("skipped"));
        diagnostic.Outcome.ShouldBe(ModelCandidateOutcome.NotEvaluated);
        diagnostic.Reason.ShouldBe("not compatible");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = ProvidersTestData.Diagnostic();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Initializer_WhenReasonIsValid_ReplacesValue()
    {
        var original = ProvidersTestData.Diagnostic();
        var copy = original with { Reason = "updated" };
        copy.Reason.ShouldBe("updated");
    }
}
