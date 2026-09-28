// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableBackendSelectionRejected"/> behavior and contracts.</summary>
public sealed class DurableBackendSelectionRejectedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableBackendSelectionRejected(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RetainsItAsASelectionResult()
    {
        var rejected = new DurableBackendSelectionRejected("no backend");

        rejected.SafeMessage.ShouldBe("no backend");
        _ = rejected.ShouldBeAssignableTo<DurableBackendSelectionResult>();
    }

    [Fact]
    public void With_WhenApplied_ProducesEqualCopy()
    {
        var rejected = new DurableBackendSelectionRejected("no backend");

        (rejected with { }).ShouldBe(rejected);
    }
}
