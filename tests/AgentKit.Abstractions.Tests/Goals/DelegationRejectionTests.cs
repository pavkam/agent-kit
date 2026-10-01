// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationRejection constraints.</summary>
public sealed class DelegationRejectionTests
{
    [Fact]
    public void Constructor_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationRejection((DelegationRejectionKind) 99, "m")).ParamName.ShouldBe("kind");

    [Fact]
    public void Constructor_WhenMessageIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(() => new DelegationRejection(DelegationRejectionKind.PolicyDenied, " ")).ParamName.ShouldBe("safeMessage");
}
