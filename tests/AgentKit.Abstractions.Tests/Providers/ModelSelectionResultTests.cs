// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Providers;

using AgentKit;

/// <summary>Verifies ModelSelectionResult derived behavior and contracts.</summary>
public sealed class ModelSelectionResultTests
{
    [Fact]
    public void ModelSelected_WhenDecisionIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new ModelSelected(null!)).ParamName.ShouldBe("decision");

    [Fact]
    public void ModelSelected_Initializer_WhenDecisionIsNull_ThrowsExactArgumentNullException()
    {
        var selected = new ModelSelected(ProvidersTestData.Decision());
        Should.Throw<ArgumentNullException>(() => selected with { Decision = null! }).ParamName.ShouldBe("Decision");
    }

    [Fact]
    public void ModelSelected_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var decision = ProvidersTestData.Decision();
        var selected = new ModelSelected(decision);
        selected.Decision.ShouldBe(decision);
    }

    [Fact]
    public void ModelSelected_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ModelSelected(ProvidersTestData.Decision());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void NoCompatibleModel_WhenRequirementsIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new NoCompatibleModel(null!, [ProvidersTestData.Diagnostic()])).ParamName.ShouldBe("requirements");

    [Fact]
    public void NoCompatibleModel_WhenDiagnosticsAreDefaultOrEmpty_ThrowsExactArgumentException()
    {
        Should.Throw<ArgumentException>(() => new NoCompatibleModel(ModelRequirements.None, default)).ParamName.ShouldBe("diagnostics");
        Should.Throw<ArgumentException>(() => new NoCompatibleModel(ModelRequirements.None, [])).ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void NoCompatibleModel_WhenDiagnosticsContainNull_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new NoCompatibleModel(ModelRequirements.None, [null!])).ParamName.ShouldBe("diagnostics");

    [Fact]
    public void NoCompatibleModel_Initializer_WhenRequirementsIsNull_ThrowsExactArgumentNullException()
    {
        var result = NoCompatibleModel();
        Should.Throw<ArgumentNullException>(() => result with { Requirements = null! }).ParamName.ShouldBe("Requirements");
    }

    [Fact]
    public void NoCompatibleModel_Initializer_WhenDiagnosticsAreInvalid_ThrowsExactArgumentException()
    {
        var result = NoCompatibleModel();
        Should.Throw<ArgumentException>(() => result with { Diagnostics = default }).ParamName.ShouldBe("Diagnostics");
        Should.Throw<ArgumentException>(() => result with { Diagnostics = [null!] }).ParamName.ShouldBe("Diagnostics");
    }

    [Fact]
    public void NoCompatibleModel_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var result = NoCompatibleModel();
        result.Requirements.ShouldBe(ModelRequirements.None);
        result.Diagnostics.ShouldBe([ProvidersTestData.Diagnostic()]);
    }

    [Fact]
    public void NoCompatibleModel_With_WhenApplied_ProducesEqualCopy()
    {
        var original = NoCompatibleModel();
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void InvalidModelPolicy_WhenReasonIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new InvalidModelPolicy(" ")).ParamName.ShouldBe("reason");

    [Fact]
    public void InvalidModelPolicy_Initializer_WhenReasonIsBlank_ThrowsExactArgumentException()
    {
        var policy = new InvalidModelPolicy("invalid");
        Should.Throw<ArgumentException>(() => policy with { Reason = " " }).ParamName.ShouldBe("Reason");
    }

    [Fact]
    public void InvalidModelPolicy_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var policy = new InvalidModelPolicy("invalid");
        policy.Reason.ShouldBe("invalid");
    }

    [Fact]
    public void InvalidModelPolicy_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InvalidModelPolicy("invalid");
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static NoCompatibleModel NoCompatibleModel() => new(ModelRequirements.None, [ProvidersTestData.Diagnostic()]);
}
