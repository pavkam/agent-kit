// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageDiagnostic behavior and contracts.</summary>
public sealed class LanguageDiagnosticTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var location = Location();
        var diagnostic = new LanguageDiagnostic(LanguageDiagnosticSeverity.Warning, "message", "CS001", "compiler", location);
        diagnostic.Severity.ShouldBe(LanguageDiagnosticSeverity.Warning);
        diagnostic.Message.ShouldBe("message");
        diagnostic.Code.ShouldBe("CS001");
        diagnostic.Source.ShouldBe("compiler");
        diagnostic.Location.ShouldBe(location);
    }

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageDiagnostic(LanguageDiagnosticSeverity.Error, " ", null, null, Location()));
        exception.ParamName.ShouldBe("message");
    }

    [Fact]
    public void Constructor_WhenSeverityIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageDiagnostic((LanguageDiagnosticSeverity) 999, "message", null, null, Location()));
        exception.ParamName.ShouldBe("severity");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var location = Location();
        var first = new LanguageDiagnostic(LanguageDiagnosticSeverity.Warning, "message", null, null, location);
        var second = new LanguageDiagnostic(LanguageDiagnosticSeverity.Warning, "message", null, null, location);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LanguageDiagnostic(LanguageDiagnosticSeverity.Warning, "message", null, null, Location());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static LanguageLocation Location() => new(new FileSystemPath("src/a.cs"), new LanguageRange(new LanguagePosition(0, 0), new LanguagePosition(0, 5)), null);
}
