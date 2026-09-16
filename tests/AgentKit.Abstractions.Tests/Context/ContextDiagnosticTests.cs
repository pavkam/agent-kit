// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextDiagnostic"/> invariants.</summary>
public sealed class ContextDiagnosticTests
{
    [Theory]
    [InlineData(null, "message", "code")]
    [InlineData("code", " ", "safeMessage")]
    public void Constructor_WhenTextIsBlank_Throws(string? code, string message, string parameter) =>
        Should.Throw<ArgumentException>(() => new ContextDiagnostic(ContextDiagnosticSeverity.Warning, code!, message)).ParamName.ShouldBe(parameter);

    [Fact]
    public void Constructor_WhenSeverityIsUndefined_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ContextDiagnostic((ContextDiagnosticSeverity) 99, "code", "message")).ParamName.ShouldBe("severity");

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var source = new ContextSourceReference(new ContextSourceNamespace("agentkit.context"), new ContextSourceKey("instructions"), new ContextSourceVersion("1.0"));
        var diagnostic = new ContextDiagnostic(ContextDiagnosticSeverity.Warning, "code", "message", source);
        diagnostic.Severity.ShouldBe(ContextDiagnosticSeverity.Warning);
        diagnostic.Code.ShouldBe("code");
        diagnostic.SafeMessage.ShouldBe("message");
        diagnostic.Source.ShouldBe(source);
    }

    [Fact]
    public void Constructor_WhenSourceIsOmitted_DefaultsToNull() =>
        new ContextDiagnostic(ContextDiagnosticSeverity.Warning, "code", "message").Source.ShouldBeNull();

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new ContextDiagnostic(ContextDiagnosticSeverity.Warning, "code", "message");
        var second = new ContextDiagnostic(ContextDiagnosticSeverity.Warning, "code", "message");
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new ContextDiagnostic(ContextDiagnosticSeverity.Warning, "code", "message");
        var copy = original with { };
        copy.ShouldBe(original);
    }
}
