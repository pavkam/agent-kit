// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies InvalidAgentDefinition behavior and contracts.</summary>
public sealed class InvalidAgentDefinitionTests
{
    [Fact]
    public void Constructor_WhenDiagnosticsIsDefaultOrEmpty_ThrowsExactParameter()
    {
        Should.Throw<ArgumentException>(() => new InvalidAgentDefinition(CompositionTestData.AgentId, default)).ParamName.ShouldBe("diagnostics");
        Should.Throw<ArgumentException>(() => new InvalidAgentDefinition(CompositionTestData.AgentId, [])).ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void Constructor_WhenDiagnosticsContainsNull_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new InvalidAgentDefinition(CompositionTestData.AgentId, [null!])).ParamName.ShouldBe("diagnostics");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var diagnostic = CompositionTestData.Diagnostic();
        var invalid = new InvalidAgentDefinition(CompositionTestData.AgentId, [diagnostic]);
        invalid.AgentId.ShouldBe(CompositionTestData.AgentId);
        invalid.Diagnostics.ShouldBe([diagnostic]);
    }

    [Fact]
    public void With_WhenDiagnosticsIsDefaultOrEmpty_ThrowsExactParameter()
    {
        var invalid = new InvalidAgentDefinition(CompositionTestData.AgentId, [CompositionTestData.Diagnostic()]);
        Should.Throw<ArgumentException>(() => _ = invalid with { Diagnostics = default }).ParamName.ShouldBe("Diagnostics");
        Should.Throw<ArgumentException>(() => _ = invalid with { Diagnostics = [] }).ParamName.ShouldBe("Diagnostics");
    }

    [Fact]
    public void With_WhenDiagnosticsContainsNull_ThrowsExactParameter()
    {
        var invalid = new InvalidAgentDefinition(CompositionTestData.AgentId, [CompositionTestData.Diagnostic()]);
        Should.Throw<ArgumentException>(() => _ = invalid with { Diagnostics = [null!] }).ParamName.ShouldBe("Diagnostics");
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new InvalidAgentDefinition(CompositionTestData.AgentId, [CompositionTestData.Diagnostic()]);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void With_WhenDiagnosticsAreValid_UpdatesDiagnostics()
    {
        var invalid = new InvalidAgentDefinition(CompositionTestData.AgentId, [CompositionTestData.Diagnostic()]);
        var updatedDiagnostic = new CompositionDiagnostic("other-code", "other message");
        var updated = invalid with { Diagnostics = [updatedDiagnostic] };
        updated.Diagnostics.ShouldBe([updatedDiagnostic]);
    }

    [Fact]
    public void Equality_WhenSameDiagnosticsArray_IsEqual()
    {
        ImmutableArray<CompositionDiagnostic> diagnostics = [CompositionTestData.Diagnostic()];
        var left = new InvalidAgentDefinition(CompositionTestData.AgentId, diagnostics);
        var right = new InvalidAgentDefinition(CompositionTestData.AgentId, diagnostics);
        left.ShouldBe(right);
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }
}
