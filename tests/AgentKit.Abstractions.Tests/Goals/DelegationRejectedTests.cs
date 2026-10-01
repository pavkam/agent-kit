// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Goals;

/// <summary>Verifies DelegationRejected constraints.</summary>
public sealed class DelegationRejectedTests
{
    [Fact]
    public void Constructor_WhenIdIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new DelegationRejected(default, new DelegationRejection(DelegationRejectionKind.PolicyDenied, "m"), ExtensionData.Empty)).ParamName.ShouldBe("id");

    [Fact]
    public void Constructor_WhenRejectionIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegationRejected(new DelegationId(Guid.NewGuid()), null!, ExtensionData.Empty)).ParamName.ShouldBe("rejection");

    [Fact]
    public void Constructor_WhenExtensionsAreNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => new DelegationRejected(
            new DelegationId(Guid.NewGuid()), new DelegationRejection(DelegationRejectionKind.PolicyDenied, "m"), null!)).ParamName.ShouldBe("extensions");
}
