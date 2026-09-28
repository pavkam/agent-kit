// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="DurabilityUnavailable"/> behavior and contracts.</summary>
public sealed class DurabilityUnavailableTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new DurabilityUnavailable(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RoundTripsIt() => new DurabilityUnavailable("no validated profile").SafeMessage.ShouldBe("no validated profile");

    [Fact]
    public void Init_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var unavailable = new DurabilityUnavailable("no validated profile");

        var exception = Should.Throw<ArgumentException>(() => unavailable with { SafeMessage = " " });

        exception.ParamName.ShouldBe(nameof(DurabilityUnavailable.SafeMessage));
    }
}
