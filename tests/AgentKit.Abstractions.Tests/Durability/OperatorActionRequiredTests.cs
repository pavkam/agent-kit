// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Durability;

using AgentKit;

/// <summary>Verifies <see cref="OperatorActionRequired"/> behavior and contracts.</summary>
public sealed class OperatorActionRequiredTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_WhenSafeMessageIsBlank_ThrowsArgumentException(string? safeMessage)
    {
        var exception = Should.Throw<ArgumentException>(() => new OperatorActionRequired(safeMessage!));

        exception.ParamName.ShouldBe("safeMessage");
    }

    [Fact]
    public void Constructor_WhenSafeMessageIsSupplied_RoundTripsIt()
    {
        new OperatorActionRequired("unknown non-idempotent effect").SafeMessage
            .ShouldBe("unknown non-idempotent effect");
    }

    [Fact]
    public void Init_WhenSafeMessageIsBlank_ThrowsArgumentException()
    {
        var required = new OperatorActionRequired("unknown non-idempotent effect");

        var exception = Should.Throw<ArgumentException>(() => required with { SafeMessage = " " });

        exception.ParamName.ShouldBe(nameof(OperatorActionRequired.SafeMessage));
    }
}
