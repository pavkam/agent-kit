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
}
