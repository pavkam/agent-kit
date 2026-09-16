// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguagePosition behavior and contracts.</summary>
public sealed class LanguagePositionTests
{
    [Fact]
    public void LanguagePosition_WhenCoordinateNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguagePosition(-1, 0));
        exception.ParamName.ShouldBe("line");
    }

    [Fact]
    public void Constructor_WhenCharacterIsNegative_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new LanguagePosition(0, -1));
        exception.ParamName.ShouldBe("character");
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var position = new LanguagePosition(3, 4);
        position.Line.ShouldBe(3);
        position.Character.ShouldBe(4);
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new LanguagePosition(1, 2);
        var second = new LanguagePosition(1, 2);
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
