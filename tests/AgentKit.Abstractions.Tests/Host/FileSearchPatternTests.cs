// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Host;



/// <summary>Verifies FileSearchPattern behavior and contracts.</summary>
public sealed class FileSearchPatternTests
{
    [Fact]
    public void FileSearchPattern_WhenRegexRequiresBacktracking_RejectsPinnedEngineMismatch()
    {
        var exception = Should.Throw<ArgumentException>(() => new FileSearchPattern(/*lang=regex*/
        "(text)\\1", FileSearchPatternKind.RegularExpression));
        exception.ParamName.ShouldBe("value");
    }

    [Fact]
    public void FileSearchPattern_WhenLiteralContainsRegexSyntax_PreservesLiteralText()
    {
        var pattern = new FileSearchPattern("[literal](text)*", FileSearchPatternKind.Literal);
        pattern.Value.ShouldBe("[literal](text)*");
        pattern.Kind.ShouldBe(FileSearchPatternKind.Literal);
    }

    [Fact]
    public void FileSearchPattern_WhenPatternExceedsComplexityBound_ThrowsForValue()
    {
        var exception = Should.Throw<ArgumentOutOfRangeException>(() => new FileSearchPattern(new string('x', FileSearchPattern.MaximumLength + 1), FileSearchPatternKind.Literal));
        exception.ParamName.ShouldBe("value");
    }
}
