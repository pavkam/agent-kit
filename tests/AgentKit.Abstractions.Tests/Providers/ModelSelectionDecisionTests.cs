// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelSelectionDecision behavior and contracts.</summary>
public sealed class ModelSelectionDecisionTests
{
    [Fact]
    public void Constructor_WhenModelIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelSelectionDecision(null!, new ModelCatalogVersion(1), "reason", [])).ParamName.ShouldBe("model");

    [Fact]
    public void Constructor_WhenReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ModelSelectionDecision(ProvidersTestData.Descriptor(), new ModelCatalogVersion(1), " ", [])).ParamName.ShouldBe("reason");

    [Fact]
    public void Constructor_WhenDiagnosticsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new ModelSelectionDecision(ProvidersTestData.Descriptor(), new ModelCatalogVersion(1), "reason", [null!])).ParamName.ShouldBe("diagnostics");

    [Fact]
    public void Initializer_WhenModelIsNull_ThrowsExactArgumentNullException()
    {
        var decision = ProvidersTestData.Decision();
        Should.Throw<ArgumentNullException>(() => decision with { Model = null! }).ParamName.ShouldBe("Model");
    }

    [Fact]
    public void Initializer_WhenReasonIsBlank_ThrowsExactArgumentException()
    {
        var decision = ProvidersTestData.Decision();
        Should.Throw<ArgumentException>(() => decision with { Reason = " " }).ParamName.ShouldBe("Reason");
    }

    [Fact]
    public void Initializer_WhenDiagnosticsContainNull_ThrowsExactArgumentException()
    {
        var decision = ProvidersTestData.Decision();
        Should.Throw<ArgumentException>(() => decision with { Diagnostics = [null!] }).ParamName.ShouldBe("Diagnostics");
    }

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var decision = ProvidersTestData.Decision();
        decision.Model.ShouldBe(ProvidersTestData.Descriptor());
        decision.CatalogVersion.ShouldBe(new ModelCatalogVersion(1));
        decision.Reason.ShouldBe("best match");
        decision.Diagnostics.ShouldBe([ProvidersTestData.Diagnostic()]);
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = ProvidersTestData.Decision();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Initializer_WhenValuesAreValid_ReplaceExistingValues()
    {
        var original = ProvidersTestData.Decision();
        var replacementModel = ProvidersTestData.Descriptor("other");
        var copy = original with { Model = replacementModel, Reason = "updated", Diagnostics = [] };
        copy.Model.ShouldBe(replacementModel);
        copy.Reason.ShouldBe("updated");
        copy.Diagnostics.ShouldBeEmpty();
    }
}
