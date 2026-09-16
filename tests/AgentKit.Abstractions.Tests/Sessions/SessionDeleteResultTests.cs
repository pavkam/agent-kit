// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionDeleteResult derived behavior and contracts.</summary>
public sealed class SessionDeleteResultTests
{
    [Fact]
    public void SessionDeleteFailed_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionDeleteFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionDeleteFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionDeleteFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("failed");
    }
}
