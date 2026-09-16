// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionPageResult derived behavior and contracts.</summary>
public sealed class SessionPageResultTests
{
    [Fact]
    public void SessionReadFailed_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionReadFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionReadFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionReadFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("failed");
    }
}
