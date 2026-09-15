// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;



/// <summary>Verifies PatchTextPlanner behavior and contracts.</summary>
public sealed class PatchTextPlannerTests
{
    [Fact]
    public void TryApply_WhenInputHasMixedNewlines_RejectsInsteadOfNormalizingUntouchedBytes()
    {
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "a", true), new ParsedPatchLine('+', "b", true),]);
        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("a\r\nb\n")], [hunk], out _, out var error);
        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("mixed newline");
    }

    [Fact]
    public void TryApply_WhenRemovedLineOnlyMatchesMidLine_RejectsAsNoMatch()
    {
        // A hunk removes whole lines; "foo" must not match the tail of the line "xfoo".
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "foo", true), new ParsedPatchLine('+', "bar", true),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("xfoo\n")], [hunk], out var final, out _);

        if (success)
        {
            Encoding.UTF8.GetString([.. final]).ShouldBe("xfoo\n", "a mid-line match must never be applied");
        }

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryApply_WhenSameBlockAppearsAsSuffixOfAnotherLine_DoesNotReportAmbiguity()
    {
        // The exact line "foo" occurs once; "xfoo" is a different line and must not count as a second match.
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "foo", true), new ParsedPatchLine('+', "bar", true),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("foo\nxfoo\n")], [hunk], out var final, out var error);

        success.ShouldBeTrue(error);
        Encoding.UTF8.GetString([.. final]).ShouldBe("bar\nxfoo\n");
    }
}
