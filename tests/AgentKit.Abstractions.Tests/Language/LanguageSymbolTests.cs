// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageSymbol behavior and contracts.</summary>
public sealed class LanguageSymbolTests
{
    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var location = Location();
        var symbol = new LanguageSymbol("Method", "method", "Container", location);
        symbol.Name.ShouldBe("Method");
        symbol.Kind.ShouldBe("method");
        symbol.ContainerName.ShouldBe("Container");
        symbol.Location.ShouldBe(location);
    }

    [Fact]
    public void Constructor_WhenNameIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageSymbol(" ", "method", null, Location()));
        exception.ParamName.ShouldBe("name");
    }

    [Fact]
    public void Constructor_WhenKindIsBlank_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageSymbol("Method", " ", null, Location()));
        exception.ParamName.ShouldBe("kind");
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var location = Location();
        var first = new LanguageSymbol("Method", "method", null, location);
        var second = new LanguageSymbol("Method", "method", null, location);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var original = new LanguageSymbol("Method", "method", null, Location());
        var copy = original with { };
        copy.ShouldBe(original);
    }

    private static LanguageLocation Location() => new(new FileSystemPath("src/a.cs"), new LanguageRange(new LanguagePosition(0, 0), new LanguagePosition(0, 5)), null);
}
