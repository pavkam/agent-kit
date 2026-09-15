// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Context;

/// <summary>Verifies <see cref="ContextDiagnosticSeverity"/> values.</summary>
public sealed class ContextDiagnosticSeverityTests
{
    [Fact]
    public void ContextDiagnosticSeverity_WhenEnumerated_ContainsNormativeValues() =>
        Enum.GetValues<ContextDiagnosticSeverity>().ShouldBe([ContextDiagnosticSeverity.Information, ContextDiagnosticSeverity.Warning, ContextDiagnosticSeverity.Error]);
}
