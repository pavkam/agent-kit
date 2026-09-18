// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted execution identity reproduces its evidence, ordered claims, and delegation ancestry.</summary>
/// <remarks>
/// Identity is authentication evidence and never authority, so the mirror is revalidated on read exactly as a fresh identity
/// is. Two invariants matter most here: storage must not become a path for widening an identity the trusted ingress
/// narrowed, and ordered collections must not degrade into default arrays the domain constructor would reject.
/// </remarks>
public sealed class JsonExecutionIdentityTests
{
    /// <summary>Verifies a null identity is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonExecutionIdentity.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies a fully populated identity reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualIdentity()
    {
        var original = TestEvidenceFactory.Identity();

        JsonExecutionIdentity.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the identity survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualIdentity()
    {
        var original = TestEvidenceFactory.Identity();

        var document = TestCanonicalJson.Cycle(JsonExecutionIdentity.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies claim and delegation order are preserved, since the domain compares both by ordered contents.</summary>
    [Fact]
    public void ToDomain_WhenIdentityCarriesCollections_PreservesOrder()
    {
        var original = TestEvidenceFactory.Identity();

        var restored = TestCanonicalJson.Cycle(JsonExecutionIdentity.FromDomain(original)).ToDomain();

        restored.Claims.Select(static claim => claim.Type)
            .ShouldBe(original.Claims.Select(static claim => claim.Type));
        restored.DelegationChain.Select(static link => link.PrincipalId)
            .ShouldBe(original.DelegationChain.Select(static link => link.PrincipalId));
    }

    /// <summary>Verifies an identity with no claims or ancestry round-trips as empty rather than default arrays.</summary>
    [Fact]
    public void ToDomain_WhenIdentityHasNoCollections_ReproducesEmptyArrays()
    {
        var original = TestEvidenceFactory.Identity(withCollections: false);

        var restored = TestCanonicalJson.Cycle(JsonExecutionIdentity.FromDomain(original)).ToDomain();

        restored.Claims.IsDefault.ShouldBeFalse();
        restored.Claims.ShouldBeEmpty();
        restored.DelegationChain.IsDefault.ShouldBeFalse();
        restored.DelegationChain.ShouldBeEmpty();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies projection never emits default arrays, which the domain constructor would reject on read.</summary>
    [Fact]
    public void FromDomain_WhenIdentityHasNoCollections_ProducesNonDefaultEmptyArrays()
    {
        var document = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity(withCollections: false));

        document.Claims.IsDefault.ShouldBeFalse();
        document.DelegationChain.IsDefault.ShouldBeFalse();
    }

    /// <summary>Verifies absent persisted arrays are normalized to empty instead of failing as default arrays.</summary>
    [Fact]
    public void ToDomain_WhenArraysAreDefault_NormalizesToEmpty()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { Claims = default, DelegationChain = default };

        var restored = document.ToDomain();

        restored.Claims.ShouldBeEmpty();
        restored.DelegationChain.ShouldBeEmpty();
    }

    /// <summary>Verifies missing evidence is refused rather than dereferenced, which a well-formed document never omits.</summary>
    [Fact]
    public void ToDomain_WhenEvidenceIsNull_ThrowsArgumentNullException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { Evidence = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Evidence");
    }

    /// <summary>Verifies a null claim element is rejected rather than dereferenced while rebuilding the identity.</summary>
    [Fact]
    public void ToDomain_WhenClaimsContainNull_ThrowsArgumentException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { Claims = [null!] };

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("Claims");
    }

    /// <summary>Verifies a null delegation element is rejected rather than dereferenced while rebuilding the ancestry.</summary>
    [Fact]
    public void ToDomain_WhenDelegationChainContainsNull_ThrowsArgumentException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { DelegationChain = [null!] };

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("DelegationChain");
    }

    /// <summary>Verifies a tampered ancestry naming another tenant fails closed instead of crossing the isolation boundary.</summary>
    [Fact]
    public void ToDomain_WhenDelegationAncestorCrossesTenant_ThrowsArgumentException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { TenantId = "tenant-other" };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies an identity claiming more assurance than an ancestor is refused rather than silently widened.</summary>
    [Fact]
    public void ToDomain_WhenAssuranceExceedsAncestor_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { Assurance = IdentityAssuranceLevel.HardwareBacked };

        var exception = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);

        exception.ParamName.ShouldBe("assurance");
    }

    /// <summary>Verifies a blank persisted principal is rejected instead of producing an unnamed subject.</summary>
    [Fact]
    public void ToDomain_WhenPrincipalIdIsBlank_ThrowsArgumentException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { PrincipalId = "  " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted version is rejected at the boundary just below the first valid value.</summary>
    [Fact]
    public void ToDomain_WhenVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var document = source with { Version = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies documents compare by ordered contents, not by immutable-array backing storage identity.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies reordering the delegation ancestry breaks equality, proving order is compared rather than ignored.</summary>
    [Fact]
    public void Equals_WhenDelegationOrderDiffers_ReturnsFalse()
    {
        var document = JsonExecutionIdentity.FromDomain(TestEvidenceFactory.Identity());
        var reordered = document with { DelegationChain = [.. document.DelegationChain.Reverse()] };

        document.Equals(reordered).ShouldBeFalse();
    }
}
