// Copyright (c) AgentKit contributors. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

namespace AgentKit.Storage.Json.Tests;

/// <summary>Verifies that a persisted grant reproduces its bounded authority exactly and never gains authorization it lacked.</summary>
/// <remarks>
/// A grant is the most consequential value this package persists. Two properties dominate these cases: a grant issued through
/// the unpinned path must not acquire snapshot-bound authorization on read, and the validity window must be reconstructed
/// through the real constructor so both cross-checking init accessors see real instants rather than a default.
/// </remarks>
public sealed class JsonSecurityGrantTests
{
    /// <summary>Verifies a null grant is rejected rather than projected as an empty document.</summary>
    [Fact]
    public void FromDomain_WhenValueIsNull_ThrowsArgumentNullException()
    {
        var exception = Should.Throw<ArgumentNullException>(() => JsonSecurityGrant.FromDomain(null!));
        exception.ParamName.ShouldBe("value");
    }

    /// <summary>Verifies an authorization-bearing grant reconstructs as an equal domain value.</summary>
    [Fact]
    public void ToDomain_WhenProjectedFromDomain_ReproducesEqualGrant()
    {
        var original = TestEvidenceFactory.Grant(withAuthorization: true);

        JsonSecurityGrant.FromDomain(original).ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies the grant survives a real canonical encode and decode, not only an in-memory projection.</summary>
    [Fact]
    public void ToDomain_WhenDecodedFromCanonicalJson_ReproducesEqualGrant()
    {
        var original = TestEvidenceFactory.Grant(withAuthorization: true);

        var document = TestCanonicalJson.Cycle(JsonSecurityGrant.FromDomain(original));

        document.ToDomain().ShouldBe(original);
    }

    /// <summary>Verifies an unpinned grant stays unpinned instead of being upgraded to snapshot-bound authority.</summary>
    [Fact]
    public void ToDomain_WhenGrantHasNoCapturedAuthorization_DoesNotGainOne()
    {
        var original = TestEvidenceFactory.Grant(withAuthorization: false);
        original.Authorization.ShouldBeNull();

        var document = JsonSecurityGrant.FromDomain(original);
        document.Authorization.ShouldBeNull();

        var restored = TestCanonicalJson.Cycle(document).ToDomain();
        restored.Authorization.ShouldBeNull();
        restored.ShouldBe(original);
    }

    /// <summary>Verifies a grant issued with captured authorization keeps it through storage rather than losing it.</summary>
    [Fact]
    public void ToDomain_WhenGrantHasCapturedAuthorization_RetainsIt()
    {
        var original = TestEvidenceFactory.Grant(withAuthorization: true);

        var restored = TestCanonicalJson.Cycle(JsonSecurityGrant.FromDomain(original)).ToDomain();

        _ = restored.Authorization.ShouldNotBeNull();
        restored.Authorization.ShouldBe(original.Authorization);
        restored.Authorization.PolicySnapshot.Version.ShouldBe(original.PolicyVersion);
    }

    /// <summary>Verifies ordered resources survive in the same order, since a grant binds to that exact sequence.</summary>
    [Fact]
    public void ToDomain_WhenGrantBindsResources_PreservesResourceOrder()
    {
        var original = TestEvidenceFactory.Grant(withAuthorization: true);

        var restored = TestCanonicalJson.Cycle(JsonSecurityGrant.FromDomain(original)).ToDomain();

        restored.Resources.Select(static resource => resource.Identifier)
            .ShouldBe(original.Resources.Select(static resource => resource.Identifier));
        restored.Resources.Select(static resource => resource.Kind)
            .ShouldBe(original.Resources.Select(static resource => resource.Kind));
    }

    /// <summary>Verifies the validity window is reconstructed intact, which only the real constructor ordering permits.</summary>
    [Fact]
    public void ToDomain_WhenValidityWindowIsPersisted_ReproducesBothInstants()
    {
        var original = TestEvidenceFactory.Grant(withAuthorization: false);

        var restored = TestCanonicalJson.Cycle(JsonSecurityGrant.FromDomain(original)).ToDomain();

        restored.NotBefore.ShouldBe(original.NotBefore);
        restored.ExpiresAt.ShouldBe(original.ExpiresAt);
        restored.AllowedUses.ShouldBe(original.AllowedUses);
    }

    /// <summary>Verifies a missing scope is refused rather than dereferenced while rebuilding the grant.</summary>
    [Fact]
    public void ToDomain_WhenScopeIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Scope = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Scope");
    }

    /// <summary>Verifies a missing identity is refused rather than dereferenced while rebuilding the grant.</summary>
    [Fact]
    public void ToDomain_WhenIdentityIsNull_ThrowsArgumentNullException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Identity = null! };

        var exception = Should.Throw<ArgumentNullException>(document.ToDomain);

        exception.ParamName.ShouldBe("Identity");
    }

    /// <summary>Verifies a null resource element is rejected rather than dereferenced while rebuilding the grant.</summary>
    [Fact]
    public void ToDomain_WhenResourcesContainNull_ThrowsArgumentException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Resources = [null!] };

        var exception = Should.Throw<ArgumentException>(document.ToDomain);

        exception.ParamName.ShouldBe("Resources");
    }

    /// <summary>Verifies an empty resource set is rejected, because unbounded authority is never a valid grant.</summary>
    [Fact]
    public void ToDomain_WhenResourcesAreEmpty_ThrowsArgumentException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Resources = [] };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies an empty persisted grant identity is rejected instead of rebuilt as a default identity.</summary>
    [Fact]
    public void ToDomain_WhenIdIsEmpty_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Id = Guid.Empty };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted audience is rejected, since any component could otherwise consume the grant.</summary>
    [Fact]
    public void ToDomain_WhenAudienceIsBlank_ThrowsArgumentException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Audience = " " };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies a blank persisted input fingerprint is rejected rather than matching any normalized input.</summary>
    [Fact]
    public void ToDomain_WhenInputFingerprintIsBlank_ThrowsArgumentException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { InputFingerprint = "\t" };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies an undefined persisted effect fails closed instead of authorizing an unknown material change.</summary>
    [Fact]
    public void ToDomain_WhenEffectIsUndefined_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { Effect = (SecurityEffect) 99 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies a non-positive persisted use bound is rejected at the boundary just below one permitted use.</summary>
    [Fact]
    public void ToDomain_WhenAllowedUsesIsZero_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { AllowedUses = 0 };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies an expiry that is not later than the start instant is rejected rather than silently inverted.</summary>
    [Fact]
    public void ToDomain_WhenExpiryIsNotLaterThanNotBefore_ThrowsArgumentOutOfRangeException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));
        var document = source with { ExpiresAt = TestEvidenceFactory.Instant };

        _ = Should.Throw<ArgumentOutOfRangeException>(document.ToDomain);
    }

    /// <summary>Verifies captured authorization that disagrees with the grant's policy version is rejected on read.</summary>
    [Fact]
    public void ToDomain_WhenAuthorizationPolicyVersionDisagrees_ThrowsArgumentException()
    {
        var source = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: true));
        var document = source with { PolicyVersion = 9 };

        _ = Should.Throw<ArgumentException>(document.ToDomain);
    }

    /// <summary>Verifies documents compare by ordered resource contents, not by immutable-array backing storage identity.</summary>
    [Fact]
    public void Equals_WhenTwoDocumentsDecodedIndependently_ReturnsTrue()
    {
        var document = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: true));

        var first = TestCanonicalJson.Cycle(document);
        var second = TestCanonicalJson.Cycle(document);

        first.ShouldBe(second);
        first.GetHashCode().ShouldBe(second.GetHashCode());
    }

    /// <summary>Verifies reordering resources breaks equality, proving order is compared rather than ignored.</summary>
    [Fact]
    public void Equals_WhenResourceOrderDiffers_ReturnsFalse()
    {
        var document = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: true));
        var reordered = document with { Resources = [.. document.Resources.Reverse()] };

        document.Equals(reordered).ShouldBeFalse();
    }

    /// <summary>Verifies the presence of captured authorization alone changes equality, since it is authority-bearing evidence.</summary>
    [Fact]
    public void Equals_WhenOneDocumentHasCapturedAuthorization_ReturnsFalse()
    {
        var pinned = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: true));
        var unpinned = JsonSecurityGrant.FromDomain(TestEvidenceFactory.Grant(withAuthorization: false));

        pinned.Equals(unpinned).ShouldBeFalse();
    }
}
