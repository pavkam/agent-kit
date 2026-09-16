// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageQueryResult behavior and contracts.</summary>
public sealed class LanguageQueryResultTests
{
    [Fact]
    public void LanguageQueryResult_WhenSuccessfulAndEmpty_PreservesSuccessfulEmptySnapshot()
    {
        var result = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [], true, null);
        result.Diagnostics.ShouldBeEmpty();
        result.Complete.ShouldBeTrue();
    }

    [Fact]
    public void LanguageQueryResult_WhenFailureHasNoMessage_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(LanguageQueryStatus.TimedOut, LanguageQueryKind.Diagnostics, null, [], [], [], false, null));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSuccessHasFailureMessage_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [], true, "unexpected"));
        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenStatusIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageQueryResult((LanguageQueryStatus) 999, LanguageQueryKind.Diagnostics, null, [], [], [], true, null));
        exception.ParamName.ShouldBe("status");
    }

    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguageQueryResult(LanguageQueryStatus.Success, (LanguageQueryKind) 999, null, [], [], [], true, null));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Constructor_WhenLocationsContainNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Definitions, null, [null!], [], [], true, null));
        exception.ParamName.ShouldBe("locations");
    }

    [Fact]
    public void Constructor_WhenSymbolsContainNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.DocumentSymbols, null, [], [null!], [], true, null));
        exception.ParamName.ShouldBe("symbols");
    }

    [Fact]
    public void Constructor_WhenDiagnosticsContainNull_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [null!], true, null));
        exception.ParamName.ShouldBe("diagnostics");
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var result = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Hover, "hover text", [], [], [], true, null);
        result.Status.ShouldBe(LanguageQueryStatus.Success);
        result.Kind.ShouldBe(LanguageQueryKind.Hover);
        result.HoverText.ShouldBe("hover text");
        result.SafeMessage.ShouldBeNull();
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [], true, null);
        var second = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [], true, null);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.Diagnostics, null, [], [], [], true, null);
        var copy = original with { };
        copy.ShouldBe(original);
    }

    [Fact]
    public void Constructor_WhenLocationsAndSymbolsAreNonEmpty_PreservesThem()
    {
        var location = new LanguageLocation(new FileSystemPath("src/a.cs"), new LanguageRange(new LanguagePosition(0, 0), new LanguagePosition(0, 5)), null);
        var symbol = new LanguageSymbol("Method", "method", null, location);
        var result = new LanguageQueryResult(LanguageQueryStatus.Success, LanguageQueryKind.DocumentSymbols, null, [location], [symbol], [], true, null);
        result.Locations.ShouldBe([location]);
        result.Symbols.ShouldBe([symbol]);
    }
}
