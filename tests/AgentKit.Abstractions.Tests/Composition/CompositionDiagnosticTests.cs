// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Composition;

/// <summary>Verifies CompositionDiagnostic behavior and contracts.</summary>
public sealed class CompositionDiagnosticTests
{
    [Fact]
    public void Constructor_WhenCodeIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new CompositionDiagnostic(" ", "message")).ParamName.ShouldBe("code");

    [Fact]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsExactParameter() =>
        Should.Throw<ArgumentException>(() => new CompositionDiagnostic("code", " ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void Constructor_WhenArgumentsAreValid_RoundTripsProperties()
    {
        var diagnostic = new CompositionDiagnostic("code", "message");
        diagnostic.Code.ShouldBe("code");
        diagnostic.SafeMessage.ShouldBe("message");
    }

    [Fact]
    public void With_WhenCodeIsBlank_ThrowsExactParameter()
    {
        var diagnostic = new CompositionDiagnostic("code", "message");
        Should.Throw<ArgumentException>(() => _ = diagnostic with { Code = " " }).ParamName.ShouldBe("Code");
    }

    [Fact]
    public void With_WhenSafeMessageIsBlank_ThrowsExactParameter()
    {
        var diagnostic = new CompositionDiagnostic("code", "message");
        Should.Throw<ArgumentException>(() => _ = diagnostic with { SafeMessage = " " }).ParamName.ShouldBe("SafeMessage");
    }

    [Fact]
    public void With_WhenValuesAreValid_UpdatesProperties()
    {
        var diagnostic = new CompositionDiagnostic("code", "message");
        var changed = diagnostic with { Code = "other-code", SafeMessage = "other message" };
        changed.Code.ShouldBe("other-code");
        changed.SafeMessage.ShouldBe("other message");
    }

    [Fact]
    public void ToString_WhenCalled_FormatsCodeAndMessage() =>
        new CompositionDiagnostic("code", "message").ToString().ShouldBe("code: message");
}
