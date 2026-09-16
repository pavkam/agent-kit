// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tests;

using System.Collections.Immutable;

/// <summary>Verifies AgentCompositionException behavior and contracts.</summary>
public sealed class AgentCompositionExceptionTests
{
    [Fact]
    public void Constructor_WhenDiagnosticsProvided_BuildsMessageFromDiagnostics()
    {
        ImmutableArray<CompositionDiagnostic> diagnostics = [new CompositionDiagnostic("agentkit.test.code", "safe message")];

        var exception = new AgentCompositionException(diagnostics);

        exception.Diagnostics.ShouldBe(diagnostics);
        exception.Message.ShouldContain("safe message");
    }

    [Fact]
    public void Constructor_WhenGivenAPlainMessage_RetainsMessageWithoutDiagnostics()
    {
        var exception = new AgentCompositionException("plain failure");

        exception.Message.ShouldBe("plain failure");
        exception.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenGivenAMessageAndInnerException_PreservesBoth()
    {
        var inner = new InvalidOperationException("cause");

        var exception = new AgentCompositionException("wrapped failure", inner);

        exception.Message.ShouldBe("wrapped failure");
        exception.InnerException.ShouldBeSameAs(inner);
        exception.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_WhenParameterless_UsesTheDefaultMessage()
    {
        var exception = new AgentCompositionException();

        exception.Message.ShouldBe("The AgentKit composition is not runnable.");
        exception.Diagnostics.ShouldBeEmpty();
    }
}
