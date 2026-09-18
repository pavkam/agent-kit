// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted delegation ancestor keeps its identities, instant, assurance, and ordered claims.</summary>
/// <remarks>
/// A delegation link is the evidence a child identity uses to prove what it inherited and what it narrowed, so ordered claim
/// content is part of the evidence rather than an incidental collection. The mirror also has to distinguish an empty claim
/// array from a default one, because the domain constructor rejects default arrays outright.
/// </remarks>
public sealed class JsonDelegationIdentityLinkTests
{
    /// <summary>Verifies a null link is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonDelegationIdentityLink.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a fully populated link reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualLink()
    {
        var original = TestEvidenceFactory.FirstDelegationLink();

        JsonDelegationIdentityLink.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the link survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualLink()
    {
        var original = TestEvidenceFactory.FirstDelegationLink();

        var document = TestCanonicalJson.Cycle(JsonDelegationIdentityLink.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies claim order is preserved exactly, since the domain compares links by ordered claim contents.</summary>
    [Fact]
    public void ToDomain_WhenLinkCarriesClaims_PreservesClaimOrder()
    {
        var original = TestEvidenceFactory.FirstDelegationLink();

        var restored = TestCanonicalJson.Cycle(JsonDelegationIdentityLink.FromDomain(original)).ToDomain();

        restored.Claims.Select(static claim => claim.Type)
            .ShouldBe(original.Claims.Select(static claim => claim.Type));
        restored.Claims.Select(static claim => claim.Value)
            .ShouldBe(original.Claims.Select(static claim => claim.Value));
    }

    /// <summary>Verifies a link that narrowed away every claim stays empty rather than being refused as a default array.</summary>
    [Fact]
    public void ToDomain_WhenLinkHasNoClaims_ReproducesEmptyClaimArray()
    {
        var original = TestEvidenceFactory.SecondDelegationLink();

        var restored = TestCanonicalJson.Cycle(JsonDelegationIdentityLink.FromDomain(original)).ToDomain();

        restored.Claims.IsDefault.ShouldBeFalse();
        restored.Claims.ShouldBeEmpty();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies projection never emits a default claim array, which the domain constructor would reject on read.</summary>
    [Fact]
    public void FromDomain_WhenLinkHasNoClaims_ProducesNonDefaultEmptyArray()
    {
        var document = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.SecondDelegationLink());

        document.Claims.IsDefault.ShouldBeFalse();
        document.Claims.ShouldBeEmpty();
    }

    /// <summary>Verifies an absent persisted claim array is normalized to empty instead of failing as a default array.</summary>
    [Fact]
    public void ToDomain_WhenClaimsArrayIsDefault_NormalizesToEmpty()
    {
        var source = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var document = source with { Claims = default };

        var restored = document.ToDomain();

        restored.Claims.IsDefault.ShouldBeFalse();
        restored.Claims.ShouldBeEmpty();
    }

    /// <summary>Verifies a null claim element is rejected rather than dereferenced while rebuilding the link.</summary>
    [Fact]
    public void ToDomain_WhenClaimsContainNull_ThrowsArgumentException()
    {
        var source = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var document = source with { Claims = [null!] };

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("Claims");
    }

    /// <summary>Verifies an empty persisted delegation identity is rejected instead of rebuilt as a default identity.</summary>
    [Fact]
    public void ToDomain_WhenIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var document = source with { Id = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted tenant is rejected, since tenant containment is a domain invariant.</summary>
    [Fact]
    public void ToDomain_WhenTenantIdIsBlank_ThrowsArgumentException()
    {
        var source = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var document = source with { TenantId = " " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted version is rejected at the boundary just below the first valid value.</summary>
    [Fact]
    public void ToDomain_WhenVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var document = source with { Version = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an assurance outside the defined range fails closed rather than widening inherited authority.</summary>
    [Fact]
    public void ToDomain_WhenAssuranceIsOutsideDefinedRange_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var document = source with { Assurance = (IdentityAssuranceLevel) 42 };

        var exception = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);

        exception.ParamName.ShouldBe("assurance");
    }

    /// <summary>Verifies documents compare by ordered claim contents, not by immutable-array backing storage identity.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies reordering claims breaks equality, proving order is compared rather than ignored.</summary>
    [Fact]
    public void Equals_WhenClaimOrderDiffers_ReturnsFalse()
    {
        var document = JsonDelegationIdentityLink.FromDomain(TestEvidenceFactory.FirstDelegationLink());
        var reordered = document with { Claims = [.. document.Claims.Reverse()] };

        document.Equals(reordered).ShouldBeFalse();
    }
}
