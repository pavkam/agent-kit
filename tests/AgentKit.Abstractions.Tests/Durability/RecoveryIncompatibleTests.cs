// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="RecoveryIncompatible"/> behavior and contracts.</summary>
public sealed class RecoveryIncompatibleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new RecoveryIncompatible(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RoundTripsIt() => new RecoveryIncompatible("unsupported version").SafeMessage.ShouldBe("unsupported version");

    [Fact]
    public void Init_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var incompatible = new RecoveryIncompatible("unsupported version");

        var exception = Should.Throw<ArgumentException>(() => incompatible with { SafeMessage = " " });

        exception.ParamName.ShouldBe(nameof(RecoveryIncompatible.SafeMessage));
    }
}
