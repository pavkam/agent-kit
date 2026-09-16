// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionCreateResult derived behavior and contracts.</summary>
public sealed class SessionCreateResultTests
{
    [Fact]
    public void SessionCreateFailed_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionCreateFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionCreateFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionCreateFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("failed");
    }
}
