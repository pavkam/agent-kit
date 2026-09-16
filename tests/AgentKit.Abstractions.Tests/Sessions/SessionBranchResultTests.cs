// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionBranchResult derived behavior and contracts.</summary>
public sealed class SessionBranchResultTests
{
    [Fact]
    public void SessionBranchFailed_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionBranchFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionBranchFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionBranchFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("failed");
    }
}
