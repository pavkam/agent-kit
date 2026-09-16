// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionLoadResult derived behavior and contracts.</summary>
public sealed class SessionLoadResultTests
{
    [Fact]
    public void SessionLoadFailed_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionLoadFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionLoadFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionLoadFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("failed");
    }
}
