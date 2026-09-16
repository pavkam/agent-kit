// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Sessions;

/// <summary>Verifies SessionAppendResult derived behavior and contracts.</summary>
public sealed class SessionAppendResultTests
{
    [Fact]
    public void SessionAppendFailed_WhenSafeMessageIsBlank_ThrowsExactArgumentException() =>
        Should.Throw<ArgumentException>(() => new SessionAppendFailed(" ")).ParamName.ShouldBe("safeMessage");

    [Fact]
    public void SessionAppendFailed_With_WhenApplied_ProducesEqualCopy()
    {
        var original = new SessionAppendFailed("failed");
        var copy = original with { };
        copy.ShouldBe(original);
        original.SafeMessage.ShouldBe("failed");
    }

    [Fact]
    public void SessionAppendNotFound_WhenAddressIsNull_ThrowsExactArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new SessionAppendNotFound(null!)).ParamName.ShouldBe("address");

    [Fact]
    public void SessionAppendNotFound_With_WhenApplied_ProducesEqualCopy()
    {
        var address = SessionsTestData.Address();
        var original = new SessionAppendNotFound(address);
        var copy = original with { };
        copy.ShouldBe(original);
        original.Address.ShouldBe(address);
    }
}
