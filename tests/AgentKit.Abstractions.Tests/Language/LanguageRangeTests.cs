// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Language;



/// <summary>Verifies LanguageRange behavior and contracts.</summary>
public sealed class LanguageRangeTests
{
    [Fact]
    public void LanguageRange_WhenEndPrecedesStart_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageRange(new LanguagePosition(2, 0), new LanguagePosition(1, 9)));
        exception.ParamName.ShouldBe("end");
    }

    [Fact]
    public void Constructor_WhenEndCharacterPrecedesStartOnSameLine_ThrowsExactParameter()
    {
        var exception = Should.Throw<ArgumentException>(() => new LanguageRange(new LanguagePosition(1, 9), new LanguagePosition(1, 5)));
        exception.ParamName.ShouldBe("end");
    }

    [Fact]
    public void Constructor_WhenCalledWithValidArguments_InitializesProperties()
    {
        var start = new LanguagePosition(1, 0);
        var end = new LanguagePosition(1, 5);
        var range = new LanguageRange(start, end);
        range.Start.ShouldBe(start);
        range.End.ShouldBe(end);
    }

    [Fact]
    public void Equals_WhenSameValues_InstancesAreEqual()
    {
        var first = new LanguageRange(new LanguagePosition(1, 0), new LanguagePosition(1, 5));
        var second = new LanguageRange(new LanguagePosition(1, 0), new LanguagePosition(1, 5));
        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }
}
