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
}
