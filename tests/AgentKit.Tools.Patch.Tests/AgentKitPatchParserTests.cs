// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Tools.Patch.Tests;



/// <summary>Verifies AgentKitPatchParser behavior and contracts.</summary>
public sealed class AgentKitPatchParserTests
{
    [Fact]
    public void TryParse_WhenPathIsDuplicated_RejectsCompletePatch()
    {
        var text = """
            *** Begin Patch
            *** Add File: same.txt
            +one
            *** Delete File: same.txt
            *** End Patch
            """;
        var success = AgentKitPatchParser.TryParse(text, 10, out var patch, out var error);
        success.ShouldBeFalse();
        patch.ShouldBeNull();
        error.ShouldNotBeNull().ShouldContain("same path");
    }

    [Fact]
    public void TryParse_WhenMoveAlsoHasHunks_RejectsUnsupportedCompoundEffect()
    {
        var text = """
            *** Begin Patch
            *** Update File: old.txt
            *** Move to: new.txt
            @@
            -old
            +new
            *** End Patch
            """;
        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);
        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("cannot also change content");
    }

    [Fact]
    public void TryParse_WhenEntryCountExceedsBound_RejectsBeforeParsingFurther()
    {
        var text = """
            *** Begin Patch
            *** Add File: one.txt
            +one
            *** Add File: two.txt
            +two
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 1, out var patch, out var error);

        success.ShouldBeFalse();
        patch.ShouldBeNull();
        error.ShouldNotBeNull().ShouldContain("entry bound");
    }

    [Fact]
    public void TryParse_WhenNoNewlineMarkerFollowsNothingInAddEntry_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Add File: one.txt
            \ No newline at end of file
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("no-newline marker must follow an added line");
    }

    [Fact]
    public void TryParse_WhenAddContentLineMissingPlusPrefix_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Add File: one.txt
            missing-prefix
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("must start with '+'");
    }

    [Fact]
    public void TryParse_WhenDeleteEntryHasBody_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Delete File: one.txt
            +unexpected
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("cannot contain a body");
    }

    [Fact]
    public void TryParse_WhenHeaderIsUnrecognized_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Rename File: one.txt
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("Expected an Add File, Update File, or Delete File header");
    }

    [Fact]
    public void TryParse_WhenMoveDestinationPathIsInvalid_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Update File: old.txt
            *** Move to: ../evil.txt
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        _ = error.ShouldNotBeNull();
    }

    [Fact]
    public void TryParse_WhenHunkSectionMissingAtAtMarker_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Update File: one.txt
            not-a-hunk-marker
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("'@@' hunk marker");
    }

    [Fact]
    public void TryParse_WhenNoNewlineMarkerFollowsNothingInHunk_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Update File: one.txt
            @@
            \ No newline at end of file
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("no-newline marker must follow a hunk line");
    }

    [Fact]
    public void TryParse_WhenHunkLineHasInvalidPrefix_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Update File: one.txt
            @@
            *invalid
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("space, '-', or '+'");
    }

    [Fact]
    public void TryParse_WhenHunkHasOnlyAddedLines_RejectsMissingContext()
    {
        var text = """
            *** Begin Patch
            *** Update File: one.txt
            @@
            +only-addition
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("requires source context or removed lines");
    }

    [Fact]
    public void TryParse_WhenUpdateEntryHasNoDestinationAndNoHunks_RejectsWithSafeMessage()
    {
        var text = """
            *** Begin Patch
            *** Update File: one.txt
            *** End Patch
            """;

        var success = AgentKitPatchParser.TryParse(text, 10, out _, out var error);

        success.ShouldBeFalse();
        error.ShouldNotBeNull().ShouldContain("requires at least one hunk");
    }

    [Fact]
    public void TryParse_WhenTextEndsWithTrailingNewline_ParsesTheSameEntries()
    {
        var text = "*** Begin Patch\n*** Add File: one.txt\n+one\n*** End Patch\n";

        var success = AgentKitPatchParser.TryParse(text, 10, out var patch, out var error);

        success.ShouldBeTrue();
        error.ShouldBeNull();
        patch.ShouldNotBeNull().Entries.ShouldHaveSingleItem().Kind.ShouldBe(ParsedPatchEntryKind.Add);
    }
}
