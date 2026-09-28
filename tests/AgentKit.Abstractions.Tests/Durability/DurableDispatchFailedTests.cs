// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableDispatchFailed"/> behavior and contracts.</summary>
public sealed class DurableDispatchFailedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableDispatchFailed(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RetainsItAsADispatchResult()
    {
        var failed = new DurableDispatchFailed("handoff refused");

        failed.SafeMessage.ShouldBe("handoff refused");
        _ = failed.ShouldBeAssignableTo<DurableDispatchResult>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var failed = new DurableDispatchFailed("handoff refused");

        (failed with { }).ShouldBe(failed);
    }
}
