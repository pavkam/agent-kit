// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurabilityRuntimeActivationFailed"/> behavior and contracts.</summary>
public sealed class DurabilityRuntimeActivationFailedTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new DurabilityRuntimeActivationFailed(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RetainsItAsAnActivationResult()
    {
        var failed = new DurabilityRuntimeActivationFailed("journal key unresolved");

        failed.SafeMessage.ShouldBe("journal key unresolved");
        _ = failed.ShouldBeAssignableTo<DurabilityRuntimeActivationResult>();
    }

    [Fact]
    public void Init_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var failed = new DurabilityRuntimeActivationFailed("journal key unresolved");

        var exception = Should.Throw<ArgumentException>(() => failed with { SafeMessage = string.Empty });

        exception.ParamName.ShouldBe(nameof(DurabilityRuntimeActivationFailed.SafeMessage));
    }
}
