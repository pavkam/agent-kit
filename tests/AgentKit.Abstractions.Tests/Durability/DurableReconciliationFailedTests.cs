// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurableReconciliationFailed"/> behavior and contracts.</summary>
public sealed class DurableReconciliationFailedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new DurableReconciliationFailed(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RetainsItAsAReconciliationResult()
    {
        var failed = new DurableReconciliationFailed("owner unreachable");

        failed.SafeMessage.ShouldBe("owner unreachable");
        _ = failed.ShouldBeAssignableTo<DurableReconciliationResult>();
    }

    [Fact]
    public void Init_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var failed = new DurableReconciliationFailed("owner unreachable");

        var exception = Should.Throw<ArgumentException>(() => failed with { SafeMessage = "  " });

        exception.ParamName.ShouldBe(nameof(DurableReconciliationFailed.SafeMessage));
    }
}
