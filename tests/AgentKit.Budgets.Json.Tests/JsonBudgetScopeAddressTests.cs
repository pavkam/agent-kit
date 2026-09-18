// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Budgets.Json.Tests;

/// <summary>Verifies the portable JSON mirror of the structural address every ledger reference is checked against.</summary>
public sealed class JsonBudgetScopeAddressTests
{
    /// <summary>Verifies projecting a null domain address is refused.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException() =>
        Should.Throw<ArgumentNullException>(() => JsonBudgetScopeAddress.FromDomain(null!)).ParamName.ShouldBe("value");

    /// <summary>Verifies an address with every optional identity absent round-trips with every optional member preserved as null.</summary>
    [Fact]
    public void ToDomain_WhenOptionalIdentitiesAreAbsent_RoundTripsWithNullMembers()
    {
        var original = new BudgetScopeAddress(
            new TenantId("tenant"), new PrincipalId("principal"), new AgentId(Guid.NewGuid()), null, null, null);

        var document = JsonBudgetScopeAddress.FromDomain(original);

        document.SessionId.ShouldBeNull();
        document.RunId.ShouldBeNull();
        document.OperationId.ShouldBeNull();
        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies an address with every optional identity present round-trips with every optional member preserved.</summary>
    [Fact]
    public void ToDomain_WhenOptionalIdentitiesArePresent_RoundTripsWithEveryMember()
    {
        var original = new BudgetScopeAddress(
            new TenantId("tenant"),
            new PrincipalId("principal"),
            new AgentId(Guid.NewGuid()),
            new SessionId(Guid.NewGuid()),
            new RunId(Guid.NewGuid()),
            new OperationId(Guid.NewGuid()));

        var document = JsonBudgetScopeAddress.FromDomain(original);

        _ = document.SessionId.ShouldNotBeNull();
        _ = document.RunId.ShouldNotBeNull();
        _ = document.OperationId.ShouldNotBeNull();
        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies a blank persisted tenant is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenTenantIsBlank_ThrowsArgumentException() =>
        Should.Throw<ArgumentException>(
            () => new JsonBudgetScopeAddress(" ", "principal", Guid.NewGuid(), null, null, null).ToDomain());

    /// <summary>Verifies an empty persisted agent identity is rejected during reconstruction.</summary>
    [Fact]
    public void ToDomain_WhenAgentIdIsEmpty_ThrowsArgumentOutOfRangeException() =>
        Should.Throw<ArgumentOutOfRangeException>(
            () => new JsonBudgetScopeAddress("tenant", "principal", Guid.Empty, null, null, null).ToDomain());

    /// <summary>Verifies the document survives an actual JSON encode and decode round trip with every optional member present.</summary>
    [Fact]
    public void Serialization_WhenRoundTripped_PreservesEveryField()
    {
        var original = new JsonBudgetScopeAddress(
            "tenant", "principal", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var options = JsonStoreSerialization.CreateCanonicalOptions();

        var decoded = JsonStoreSerialization.Decode<JsonBudgetScopeAddress>(
            JsonStoreSerialization.Encode(original, options, 1_024), options);

        decoded.ShouldBe(original);
    }
}
