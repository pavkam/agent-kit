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
}
