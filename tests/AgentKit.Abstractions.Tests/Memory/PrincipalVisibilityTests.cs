// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Abstractions.Tests.Memory;

/// <summary>Verifies <see cref="PrincipalVisibility"/> constraints and value semantics.</summary>
public sealed class PrincipalVisibilityTests
{
    [Fact]
    public void Constructor_WhenTenantIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PrincipalVisibility(default, new PrincipalId("p"))).ParamName.ShouldBe("tenantId");

    [Fact]
    public void Constructor_WhenOwnerIsDefault_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PrincipalVisibility(new TenantId("t"), default)).ParamName.ShouldBe("ownerPrincipalId");

    [Fact]
    public void Constructor_WhenOmittedSharing_IsPrivateToTheOwner() =>
        new PrincipalVisibility(new TenantId("t"), new PrincipalId("p")).SharedWithTenant.ShouldBeFalse();

    [Fact]
    public void Equality_WhenPartsMatch_IsStructural() =>
        new PrincipalVisibility(new TenantId("t"), new PrincipalId("p"), true)
            .ShouldBe(new PrincipalVisibility(new TenantId("t"), new PrincipalId("p"), true));
}
