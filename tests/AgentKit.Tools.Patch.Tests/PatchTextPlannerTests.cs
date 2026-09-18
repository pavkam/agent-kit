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
    public void TryApply_WhenRemovedUnterminatedLineOnlyMatchesALinePrefix_RejectsAsNoMatch()
    {
        // IndexOfLineAnchoredBlock only anchored the start of the old block at a line boundary. When the
        // hunk's last source line carries the "\ No newline at end of file" marker (HasTerminator: false), the
        // old block ends without '\n', so "foo" must not match the first three characters of "foobar\n": that
        // is a different, longer line than the one the hunk describes, and applying anyway would silently
        // leave "bar\n" behind instead of rejecting the hunk as not matching.
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "foo", false), new ParsedPatchLine('+', "baz", false),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("foobar\n")], [hunk], out var final, out _);

        if (success)
        {
            Encoding.UTF8.GetString([.. final]).ShouldBe("foobar\n", "a line-prefix match on an unterminated block must never be applied");
        }

        success.ShouldBeFalse();
    }

    [Fact]
    public void TryApply_WhenRemovedUnterminatedLineIsTheFilesFinalLine_Matches()
    {
        // The positive counterpart: when the old block's unterminated line genuinely reaches the end of the
        // file, the match is legitimate and must still be applied.
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "foo", false), new ParsedPatchLine('+', "baz", false),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("foo")], [hunk], out var final, out var error);

        success.ShouldBeTrue(error);
        Encoding.UTF8.GetString([.. final]).ShouldBe("baz");
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

    [Fact]
    public void TryApply_WhenContentIsNotValidUtf8_RejectsWithSafeMessage()
    {
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "a", true), new ParsedPatchLine('+', "b", true),]);

        var success = PatchTextPlanner.TryApply([0xff], [hunk], out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("strict UTF-8");
    }

    [Fact]
    public void TryApply_WhenContentContainsBinaryNul_RejectsWithSafeMessage()
    {
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "a", true), new ParsedPatchLine('+', "b", true),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("a\0b")], [hunk], out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("binary NUL");
    }

    [Fact]
    public void TryApply_WhenContentHasBareCarriageReturn_RejectsWithSafeMessage()
    {
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "a", true), new ParsedPatchLine('+', "b", true),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("a\rb")], [hunk], out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("bare carriage returns");
    }

    [Fact]
    public void TryApply_WhenAddedLineContainsInvalidUnicodeScalar_RejectsWithSafeMessage()
    {
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('-', "old", true), new ParsedPatchLine('+', "\uD800", true),]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("old\n")], [hunk], out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("invalid Unicode scalar data");
    }

    [Fact]
    public void TryApply_WhenHunkHasOnlyAdditionsAndSearchExceedsTextLength_StillMatchesAtStart()
    {
        // A hunk built from only '+' lines has an empty old block; the ambiguity check then advances its
        // internal search index past the end of a single-line, unterminated source before finding no
        // second match, exercising the loop's own bounds exit rather than an early no-match return.
        var hunk = new ParsedPatchHunk([new ParsedPatchLine('+', "appended", true)]);

        var success = PatchTextPlanner.TryApply([.. Encoding.UTF8.GetBytes("abc")], [hunk], out var final, out var error);

        success.ShouldBeTrue(error);
        Encoding.UTF8.GetString([.. final]).ShouldBe("appended\nabc");
    }
}
