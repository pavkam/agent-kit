// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that persisted enforcement evidence reproduces the concrete effect and never borrows missing authorization.</summary>
/// <remarks>
/// Enforcement evidence proves that the effect about to happen matches the effect that was authorized, so every member is
/// recomputed by the effecting boundary rather than copied from a grant. The mirror must preserve that independence: a legacy
/// enforcement path presents no captured context, and a round trip must not invent one.
/// </remarks>
public sealed class JsonSecurityEnforcementRequestTests
{
    /// <summary>Verifies null enforcement evidence is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonSecurityEnforcementRequest.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies authorization-bearing enforcement evidence reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualEnforcementRequest()
    {
        var original = TestEvidenceFactory.Enforcement(withAuthorization: true);

        JsonSecurityEnforcementRequest.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the evidence survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualEnforcementRequest()
    {
        var original = TestEvidenceFactory.Enforcement(withAuthorization: true);

        var document = TestCanonicalJson.Cycle(JsonSecurityEnforcementRequest.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies legacy enforcement evidence stays unpinned instead of gaining snapshot-bound authorization.</summary>
    [Fact]
    public void ToDomain_WhenEvidenceHasNoCapturedAuthorization_DoesNotGainOne()
    {
        var original = TestEvidenceFactory.Enforcement(withAuthorization: false);
        original.Authorization.ShouldBeNull();

        var document = JsonSecurityEnforcementRequest.FromDomain(original);
        document.Authorization.ShouldBeNull();

        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.Authorization.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies evidence presented with captured authorization keeps it through storage rather than losing it.</summary>
    [Fact]
    public void ToDomain_WhenEvidenceHasCapturedAuthorization_RetainsIt()
    {
        var original = TestEvidenceFactory.Enforcement(withAuthorization: true);

        var restored = TestCanonicalJson.Cycle(JsonSecurityEnforcementRequest.FromDomain(original)).ToDomain();

        _ = restored.Authorization.ShouldNotBeNull();
        restored.Authorization.ShouldBe(original.Authorization);
        restored.Authorization.Scope.ShouldBe(restored.Scope);
    }

    /// <summary>Verifies concrete resources survive in the same order, since enforcement compares that exact sequence.</summary>
    [Fact]
    public void ToDomain_WhenEvidenceNamesResources_PreservesResourceOrder()
    {
        var original = TestEvidenceFactory.Enforcement(withAuthorization: false);

        var restored = TestCanonicalJson.Cycle(JsonSecurityEnforcementRequest.FromDomain(original)).ToDomain();

        restored.Resources.Select(static resource => resource.Identifier)
            .ShouldBe(original.Resources.Select(static resource => resource.Identifier));
    }

    /// <summary>Verifies a missing scope is refused rather than dereferenced while rebuilding the evidence.</summary>
    [Fact]
    public void ToDomain_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { Scope = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Scope");
    }

    /// <summary>Verifies a missing identity is refused rather than dereferenced while rebuilding the evidence.</summary>
    [Fact]
    public void ToDomain_WhenIdentityIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { Identity = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Identity");
    }

    /// <summary>Verifies a null resource element is rejected rather than dereferenced while rebuilding the evidence.</summary>
    [Fact]
    public void ToDomain_WhenResourcesContainNull_ThrowsArgumentException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { Resources = [null!] };

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("Resources");
    }

    /// <summary>Verifies an empty resource set is rejected, because an effect must name what it touches.</summary>
    [Fact]
    public void ToDomain_WhenResourcesAreEmpty_ThrowsArgumentException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { Resources = [] };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted audience is rejected, since the effecting component must be named exactly.</summary>
    [Fact]
    public void ToDomain_WhenAudienceIsBlank_ThrowsArgumentException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { Audience = "  " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted input fingerprint is rejected rather than matching any concrete input.</summary>
    [Fact]
    public void ToDomain_WhenInputFingerprintIsBlank_ThrowsArgumentException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { InputFingerprint = "\t" };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies an undefined persisted operation kind fails closed instead of enforcing an unknown boundary.</summary>
    [Fact]
    public void ToDomain_WhenKindIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { Kind = (SecurityOperationKind) 99 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted revocation epoch is rejected at the boundary just below the first epoch.</summary>
    [Fact]
    public void ToDomain_WhenRevocationVersionIsZero_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var document = source with { RevocationVersion = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies documents compare by ordered resource contents, not by immutable-array backing storage identity.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityEnforcementRequest.FromDomain(
            TestEvidenceFactory.Enforcement(withAuthorization: true));

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies reordering resources breaks equality, proving order is compared rather than ignored.</summary>
    [Fact]
    public void Equals_WhenResourceOrderDiffers_ReturnsFalse()
    {
        var document = JsonSecurityEnforcementRequest.FromDomain(Sample());
        var reordered = document with { Resources = [.. document.Resources.Reverse()] };

        document.Equals(reordered).ShouldBeFalse();
    }

    private static SecurityEnforcementRequest Sample() =>
        TestEvidenceFactory.Enforcement(withAuthorization: false);
}
